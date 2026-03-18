namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Buffers;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.Intrinsics;
    using System.Runtime.Intrinsics.X86;
    using System.Text;
    using System.Threading.Tasks;
    using Internal;
    using Resources;

    /// <summary>Provides functionality for encoding data into Base-10 (decimal) text representations and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Each byte maps independently to three decimal chars with no carry state, making it fully parallelizable across all cores with AVX2 and AVX-512 SIMD acceleration.</remarks>
    public sealed class Base10 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1024 * 1024;
        private const uint MagicOf100 = 656u;
        private const uint MagicOf10 = 52429u;

        private static readonly Vector256<byte> DigitLut256 =
            Vector256.Create((byte)
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0);

        private static readonly Vector512<byte> DigitLut512 =
            Vector512.Create((byte)
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 0, 0, 0, 0, 0, 0);

        private static readonly int[] ReverseTable = BuildReverseTable();

        /// <summary>Initializes a new instance of the <see cref="Base10"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base10() { }

        /// <inheritdoc/>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputStream);
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var threadCount = Environment.ProcessorCount;
                var inputSize = ChunkSize * threadCount;
                var useAvx512 = Avx512BW.IsSupported;
                var useAvx2 = Avx2.IsSupported;

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];

                byte[][] outputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize * 3),
                    ArrayPool<byte>.Shared.Rent(inputSize * 3)
                ];
                int[][] chunkSizes = [new int[threadCount], new int[threadCount]];

                try
                {
                    Task pending = null;
                    var cur = 0;
                    var linePos = 0;
                    var totalRead = ReadBuffer(bsi, inputBufs[cur]);

                    while (totalRead > 0)
                    {
                        var isLastBatch = totalRead < inputSize;
                        var numChunks = Math.Min(threadCount, (totalRead + ChunkSize - 1) / ChunkSize);
                        var read = totalRead;
                        var slot = cur;

                        Parallel.For(0, numChunks, i =>
                        {
                            var inputStart = i * ChunkSize;
                            var inputLen = Math.Min(ChunkSize, read - inputStart);
                            var outStart = i * ChunkSize * 3;
                            var ob = outputBufs[slot];
                            var input = inputBufs[slot];

                            if (useAvx512 && inputLen >= 64)
                            {
                                unsafe
                                {
                                    fixed (byte* pIn = &input[inputStart])
                                        fixed (byte* pOut = &ob[outStart])
                                        {
                                            var avx512Count = inputLen / 64 * 64;
                                            EncodeAvx512(pIn, pOut, avx512Count);

                                            if (useAvx2 && inputLen - avx512Count >= 32)
                                            {
                                                var avx2Count = (inputLen - avx512Count) / 32 * 32;
                                                EncodeAvx2(pIn + avx512Count, pOut + avx512Count * 3, avx2Count);
                                                EncodeScalar(input, inputStart + avx512Count + avx2Count, inputLen - avx512Count - avx2Count, ob, outStart + (avx512Count + avx2Count) * 3);
                                            }
                                            else
                                                EncodeScalar(input, inputStart + avx512Count, inputLen - avx512Count, ob, outStart + avx512Count * 3);
                                        }
                                }
                            }
                            else if (useAvx2 && inputLen >= 32)
                            {
                                unsafe
                                {
                                    fixed (byte* pIn = &input[inputStart])
                                        fixed (byte* pOut = &ob[outStart])
                                        {
                                            var avx2Count = inputLen / 32 * 32;
                                            EncodeAvx2(pIn, pOut, avx2Count);
                                            EncodeScalar(input, inputStart + avx2Count, inputLen - avx2Count, ob, outStart + avx2Count * 3);
                                        }
                                }
                            }
                            else
                                EncodeScalar(input, inputStart, inputLen, ob, outStart);

                            chunkSizes[slot][i] = inputLen * 3;
                        });

                        pending?.Wait();

                        var capturedLinePos = linePos;
                        pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                        {
                            var pos = capturedLinePos;
                            for (var j = 0; j < numChunks; j++)
                            {
                                var start = j * ChunkSize * 3;
                                var count = chunkSizes[slot][j];
                                if (lineLength < 1)
                                {
                                    bso.Write(outputBufs[slot], start, count);
                                    continue;
                                }
                                var end = start + count;
                                var src = start;
                                while (src < end)
                                {
                                    var chunk = Math.Min(lineLength - pos, end - src);
                                    bso.Write(outputBufs[slot], src, chunk);
                                    src += chunk;
                                    pos += chunk;
                                    if (pos < lineLength)
                                        continue;
                                    bso.Write(Separator.Span);
                                    pos = 0;
                                }
                            }
                            linePos = pos;
                        });

                        if (isLastBatch)
                            break;

                        cur ^= 1;
                        totalRead = ReadBuffer(bsi, inputBufs[cur]);
                    }

                    pending?.Wait();
                }
                finally
                {
                    foreach (var b in inputBufs)
                        ArrayPool<byte>.Shared.Return(b);
                    foreach (var b in outputBufs)
                        ArrayPool<byte>.Shared.Return(b);
                }
            }
            finally
            {
                if (dispose)
                {
                    bsi.Dispose();
                    bso.Dispose();
                }
                else
                {
                    bsi.Flush();
                    bso.Flush();
                }
            }
        }

        /// <inheritdoc/>
        public override void DecodeStream(Stream inputStream, Stream outputStream, bool dispose = false)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputStream);
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var threadCount = Environment.ProcessorCount;
                var inputSize = ChunkSize * 3 * threadCount;

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];
                byte[][] compactBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize + 4),
                    ArrayPool<byte>.Shared.Rent(inputSize + 4)
                ];
                byte[][] outputBufs = [null, null];

                int[][] chunkSizes = [new int[threadCount], new int[threadCount]];
                int[][] boundaries = [new int[threadCount + 1], new int[threadCount + 1]];

                Span<byte> leftover = stackalloc byte[4];
                var leftoverLen = 0;

                try
                {
                    Task pending = null;
                    var cur = 0;
                    var bytesRead = ReadBuffer(bsi, inputBufs[cur]);

                    while (bytesRead > 0 || leftoverLen > 0)
                    {
                        var isLastBatch = bytesRead < inputSize;
                        var slot = cur;

                        leftover[..leftoverLen].CopyTo(compactBufs[slot]);
                        var compactLen = leftoverLen;
                        leftoverLen = 0;

                        for (var i = 0; i < bytesRead; i++)
                        {
                            var b = inputBufs[slot][i];
                            if (IsSkippable(b) || Separator.Span.Contains(b) || b is (byte)'-' or (byte)',')
                                continue;
                            if (ReverseTable[b] == -1)
                                throw new DecoderFallbackException(ExceptionMessages.CharsInStreamAreInvalid);
                            compactBufs[slot][compactLen++] = b;
                        }

                        var remainder = compactLen % 3;
                        if (!isLastBatch && remainder > 0)
                        {
                            compactBufs[slot].AsSpan(compactLen - remainder, remainder).CopyTo(leftover);
                            leftoverLen = remainder;
                            compactLen -= remainder;
                        }

                        var totalTriples = compactLen / 3;
                        var numChunks = 0;

                        if (totalTriples > 0)
                        {
                            numChunks = Math.Min(threadCount, totalTriples);
                            var gpc = (totalTriples + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 4);
                            }

                            var bounds = boundaries[slot];
                            bounds[0] = 0;
                            for (var i = 1; i <= numChunks; i++)
                                bounds[i] = Math.Min(i * gpc, totalTriples);

                            var sizes = chunkSizes[slot];
                            var cBuf = compactBufs[slot];
                            var ob = outputBufs[slot];

                            Parallel.For(0, numChunks, i =>
                            {
                                var startTriple = bounds[i];
                                var endTriple = bounds[i + 1];
                                var outPos = startTriple;

                                for (var p = startTriple; p < endTriple; p++)
                                {
                                    var h = ReverseTable[cBuf[p * 3]];
                                    var t = ReverseTable[cBuf[p * 3 + 1]];
                                    var o = ReverseTable[cBuf[p * 3 + 2]];
                                    ob[outPos++] = (byte)(h * 100 + t * 10 + o);
                                }

                                sizes[i] = outPos - startTriple;
                            });
                        }

                        pending?.Wait();
                        pending = null;

                        if (totalTriples > 0)
                        {
                            var num = numChunks;
                            var bounds = boundaries[slot];
                            pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                            {
                                for (var i = 0; i < num; i++)
                                    bso.Write(outputBufs[slot], bounds[i], chunkSizes[slot][i]);
                            });
                        }

                        if (bytesRead == 0)
                            break;

                        cur ^= 1;
                        bytesRead = ReadBuffer(bsi, inputBufs[cur]);
                    }

                    pending?.Wait();
                }
                finally
                {
                    foreach (var b in inputBufs)
                        ArrayPool<byte>.Shared.Return(b);
                    foreach (var b in compactBufs)
                        ArrayPool<byte>.Shared.Return(b);
                    foreach (var b in outputBufs)
                        if (b != null)
                            ArrayPool<byte>.Shared.Return(b);
                }
            }
            finally
            {
                if (dispose)
                {
                    bsi.Dispose();
                    bso.Dispose();
                }
                else
                {
                    bsi.Flush();
                    bso.Flush();
                }
            }
        }

        private static int[] BuildReverseTable()
        {
            var table = new int[256];
            Array.Fill(table, -1);
            for (var i = 0; i <= 9; i++)
                table['0' + i] = i;
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx512(byte* input, byte* output, int count)
        {
            var lut = DigitLut512;
            var c100 = Vector512.Create((ushort)MagicOf100);
            var c10 = Vector512.Create((ushort)MagicOf10);
            var processed = 0;

            Span<byte> hb = stackalloc byte[64];
            Span<byte> tb = stackalloc byte[64];
            Span<byte> ob = stackalloc byte[64];

            while (processed + 64 <= count)
            {
                var raw = Avx512F.LoadVector512(input + processed);

                var lo16 = Avx512BW.UnpackLow(raw, Vector512<byte>.Zero).AsUInt16();
                var hi16 = Avx512BW.UnpackHigh(raw, Vector512<byte>.Zero).AsUInt16();

                var hLo = Avx512BW.MultiplyHigh(lo16, c100);
                var hHi = Avx512BW.MultiplyHigh(hi16, c100);

                var rLo = Avx512BW.Subtract(lo16, Avx512BW.MultiplyLow(hLo, Vector512.Create((ushort)100)));
                var rHi = Avx512BW.Subtract(hi16, Avx512BW.MultiplyLow(hHi, Vector512.Create((ushort)100)));

                var tLo = Avx512BW.ShiftRightLogical(Avx512BW.MultiplyHigh(rLo, c10), 3);
                var tHi = Avx512BW.ShiftRightLogical(Avx512BW.MultiplyHigh(rHi, c10), 3);

                var oLo = Avx512BW.Subtract(rLo, Avx512BW.MultiplyLow(tLo, Vector512.Create((ushort)10)));
                var oHi = Avx512BW.Subtract(rHi, Avx512BW.MultiplyLow(tHi, Vector512.Create((ushort)10)));

                var h = Avx512BW.PackUnsignedSaturate(hLo.AsInt16(), hHi.AsInt16());
                var t = Avx512BW.PackUnsignedSaturate(tLo.AsInt16(), tHi.AsInt16());
                var o = Avx512BW.PackUnsignedSaturate(oLo.AsInt16(), oHi.AsInt16());

                var hc = Avx512BW.Shuffle(lut, h);
                var tc = Avx512BW.Shuffle(lut, t);
                var oc = Avx512BW.Shuffle(lut, o);

                hc.StoreUnsafe(ref hb[0]);
                tc.StoreUnsafe(ref tb[0]);
                oc.StoreUnsafe(ref ob[0]);

                for (var j = 0; j < 64; j++)
                {
                    output[processed * 3 + j * 3] = hb[j];
                    output[processed * 3 + j * 3 + 1] = tb[j];
                    output[processed * 3 + j * 3 + 2] = ob[j];
                }

                processed += 64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx2(byte* input, byte* output, int count)
        {
            var lut = DigitLut256;
            var c100Lo = Vector256.Create((ushort)MagicOf100);
            var c10Lo = Vector256.Create((ushort)MagicOf10);
            Vector256.Create((byte)100);
            Vector256.Create((byte)10);
            var processed = 0;

            Span<byte> hb = stackalloc byte[32];
            Span<byte> tb = stackalloc byte[32];
            Span<byte> ob = stackalloc byte[32];

            while (processed + 32 <= count)
            {
                var raw = Avx.LoadVector256(input + processed);

                var lo16 = Avx2.UnpackLow(raw, Vector256<byte>.Zero).AsUInt16();
                var hi16 = Avx2.UnpackHigh(raw, Vector256<byte>.Zero).AsUInt16();

                var hLo = Avx2.ShiftRightLogical(Avx2.MultiplyHigh(lo16, c100Lo), 0).AsUInt16();
                var hHi = Avx2.ShiftRightLogical(Avx2.MultiplyHigh(hi16, c100Lo), 0).AsUInt16();

                var rLo = Avx2.Subtract(lo16, Avx2.MultiplyLow(hLo, Vector256.Create((ushort)100))).AsUInt16();
                var rHi = Avx2.Subtract(hi16, Avx2.MultiplyLow(hHi, Vector256.Create((ushort)100))).AsUInt16();
                var tLo = Avx2.ShiftRightLogical(Avx2.MultiplyHigh(rLo, c10Lo), 3);
                var tHi = Avx2.ShiftRightLogical(Avx2.MultiplyHigh(rHi, c10Lo), 3);

                var oLo = Avx2.Subtract(rLo, Avx2.MultiplyLow(tLo, Vector256.Create((ushort)10)));
                var oHi = Avx2.Subtract(rHi, Avx2.MultiplyLow(tHi, Vector256.Create((ushort)10)));

                var h = Avx2.PackUnsignedSaturate(hLo.AsInt16(), hHi.AsInt16());
                var t = Avx2.PackUnsignedSaturate(tLo.AsInt16(), tHi.AsInt16());
                var o = Avx2.PackUnsignedSaturate(oLo.AsInt16(), oHi.AsInt16());

                var hc = Avx2.Shuffle(lut, h);
                var tc = Avx2.Shuffle(lut, t);
                var oc = Avx2.Shuffle(lut, o);

                hc.StoreUnsafe(ref hb[0]);
                tc.StoreUnsafe(ref tb[0]);
                oc.StoreUnsafe(ref ob[0]);

                for (var j = 0; j < 16; j++)
                {
                    output[processed * 3 + j * 3] = hb[j];
                    output[processed * 3 + j * 3 + 1] = tb[j];
                    output[processed * 3 + j * 3 + 2] = ob[j];
                }
                for (var j = 0; j < 16; j++)
                {
                    output[processed * 3 + 48 + j * 3] = hb[j + 16];
                    output[processed * 3 + 48 + j * 3 + 1] = tb[j + 16];
                    output[processed * 3 + 48 + j * 3 + 2] = ob[j + 16];
                }

                processed += 32;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeScalar(byte[] input, int start, int count, byte[] output, int outStart)
        {
            for (var i = 0; i < count; i++)
            {
                var b = (uint)input[start + i];
                var h = b * MagicOf100 >> 16;
                var r = b - h * 100;
                var t = r * MagicOf10 >> 19;
                var o = r - t * 10;
                output[outStart + i * 3] = (byte)('0' + h);
                output[outStart + i * 3 + 1] = (byte)('0' + t);
                output[outStart + i * 3 + 2] = (byte)('0' + o);
            }
        }
    }
}
