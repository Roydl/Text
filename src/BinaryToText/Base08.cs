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

    /// <summary>Provides functionality for encoding data into Base-8 (octal) text representations and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Each byte maps independently to three octal digits via pure bit-shifts with no division, making it fully parallelizable across all cores with AVX2 and AVX-512 SIMD acceleration. The 3-byte output stride limits SIMD scatter efficiency, placing throughput roughly on par with Base10 and approximately 7x below Base16.</remarks>
    public sealed class Base08 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1024 * 1024;
        private static readonly Vector256<byte> Vec3Of256 = Vector256.Create((byte)3);
        private static readonly Vector512<byte> Vec3Of512 = Vector512.Create((byte)3);
        private static readonly Vector256<byte> Vec48Of256 = Vector256.Create((byte)48);
        private static readonly Vector512<byte> Vec48Of512 = Vector512.Create((byte)48);
        private static readonly Vector256<byte> Vec7Of256 = Vector256.Create((byte)7);
        private static readonly Vector512<byte> Vec7Of512 = Vector512.Create((byte)7);
        private static readonly int[] ReverseTable = BuildReverseTable();

        /// <summary>Initializes a new instance of the <see cref="Base08"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base08() { }

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

                        // Phase 1: Compact — prepend leftover, strip whitespace, separators, validate
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

                        // Carry incomplete triple into the next batch
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
                                    var d2 = ReverseTable[cBuf[p * 3]];
                                    var d1 = ReverseTable[cBuf[p * 3 + 1]];
                                    var d0 = ReverseTable[cBuf[p * 3 + 2]];
                                    ob[outPos++] = (byte)(d2 << 6 | d1 << 3 | d0);
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
            for (var i = 0; i <= 7; i++)
                table['0' + i] = i;
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx512(byte* input, byte* output, int count)
        {
            var v48 = Vec48Of512;
            var v7 = Vec7Of512;
            var v3 = Vec3Of512;
            var processed = 0;

            Span<byte> s2 = stackalloc byte[64];
            Span<byte> s1 = stackalloc byte[64];
            Span<byte> s0 = stackalloc byte[64];

            while (processed + 64 <= count)
            {
                var raw = Avx512F.LoadVector512(input + processed);

                var d2 = Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 6).AsByte(), v3);
                var d1 = Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 3).AsByte(), v7);
                var d0 = Avx512F.And(raw, v7);

                var c2 = Avx512BW.Add(d2, v48);
                var c1 = Avx512BW.Add(d1, v48);
                var c0 = Avx512BW.Add(d0, v48);

                c2.StoreUnsafe(ref s2[0]);
                c1.StoreUnsafe(ref s1[0]);
                c0.StoreUnsafe(ref s0[0]);

                for (var j = 0; j < 64; j++)
                {
                    output[processed * 3 + j * 3] = s2[j];
                    output[processed * 3 + j * 3 + 1] = s1[j];
                    output[processed * 3 + j * 3 + 2] = s0[j];
                }

                processed += 64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx2(byte* input, byte* output, int count)
        {
            var v48 = Vec48Of256;
            var v7 = Vec7Of256;
            var v3 = Vec3Of256;
            var processed = 0;

            Span<byte> b2 = stackalloc byte[32];
            Span<byte> b1 = stackalloc byte[32];
            Span<byte> b0 = stackalloc byte[32];

            while (processed + 32 <= count)
            {
                var raw = Avx.LoadVector256(input + processed);

                var d2 = Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 6).AsByte(), v3);
                var d1 = Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 3).AsByte(), v7);
                var d0 = Avx2.And(raw, v7);

                var c2 = Avx2.Add(d2, v48);
                var c1 = Avx2.Add(d1, v48);
                var c0 = Avx2.Add(d0, v48);

                c2.StoreUnsafe(ref b2[0]);
                c1.StoreUnsafe(ref b1[0]);
                c0.StoreUnsafe(ref b0[0]);

                for (var j = 0; j < 32; j++)
                {
                    output[processed * 3 + j * 3] = b2[j];
                    output[processed * 3 + j * 3 + 1] = b1[j];
                    output[processed * 3 + j * 3 + 2] = b0[j];
                }

                processed += 32;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeScalar(byte[] input, int start, int count, byte[] output, int outStart)
        {
            for (var i = 0; i < count; i++)
            {
                var b = input[start + i];
                output[outStart + i * 3] = (byte)('0' + (b >> 6));
                output[outStart + i * 3 + 1] = (byte)('0' + (b >> 3 & 7));
                output[outStart + i * 3 + 2] = (byte)('0' + (b & 7));
            }
        }
    }
}
