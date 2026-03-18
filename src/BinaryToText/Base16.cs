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

    /// <summary>Provides functionality for encoding data into Base-16 (hexadecimal) text representations and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Each byte maps independently to two hex chars with no carry state, making Base16 the most SIMD-friendly of all binary-to-text encodings. Encoding uses AVX-512BW (64 bytes/iter) or AVX2 (32 bytes/iter) with parallel processing across all cores.</remarks>
    public sealed class Base16 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1024 * 1024;

        // Hex LUT broadcast to both 128-bit lanes — vpshufb maps nibble 0-15 to lowercase hex char.
        // Broadcast is required: vpshufb shuffles within each 128-bit lane independently,
        // so both lanes need the full 16-entry table.
        private static readonly Vector256<byte> HexLut256 =
            Vector256.Create((byte)
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102);

        // Same LUT for AVX-512 (same 16-byte pattern repeated across all 4 × 128-bit lanes)
        private static readonly Vector512<byte> HexLut512 =
            Vector512.Create((byte)
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
                             48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102);

        private static readonly Vector256<byte> MaskOf256 = Vector256.Create((byte)0x0f);
        private static readonly Vector512<byte> MaskOf512 = Vector512.Create((byte)0x0f);

        // Reverse lookup: ASCII value → nibble (0-15), -1 if not a valid hex char
        private static readonly int[] ReverseTable = BuildReverseTable();

        /// <summary>Initializes a new instance of the <see cref="Base16"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base16() { }

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
                    ArrayPool<byte>.Shared.Rent(inputSize * 2),
                    ArrayPool<byte>.Shared.Rent(inputSize * 2)
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
                            var outStart = i * ChunkSize * 2;
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
                                                EncodeAvx2(pIn + avx512Count, pOut + avx512Count * 2, avx2Count);
                                                EncodeScalar(input, inputStart + avx512Count + avx2Count, inputLen - avx512Count - avx2Count, ob, outStart + (avx512Count + avx2Count) * 2);
                                            }
                                            else
                                                EncodeScalar(input, inputStart + avx512Count, inputLen - avx512Count, ob, outStart + avx512Count * 2);
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
                                            EncodeScalar(input, inputStart + avx2Count, inputLen - avx2Count, ob, outStart + avx2Count * 2);
                                        }
                                }
                            }
                            else
                                EncodeScalar(input, inputStart, inputLen, ob, outStart);

                            chunkSizes[slot][i] = inputLen * 2;
                        });

                        pending?.Wait();

                        var capturedLinePos = linePos;
                        pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                        {
                            var pos = capturedLinePos;
                            for (var j = 0; j < numChunks; j++)
                            {
                                var start = j * ChunkSize * 2;
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
                var inputSize = ChunkSize * 2 * threadCount;

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];
                byte[][] compactBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize + 2),
                    ArrayPool<byte>.Shared.Rent(inputSize + 2)
                ];
                byte[][] outputBufs = [null, null];

                int[][] chunkSizes = [new int[threadCount], new int[threadCount]];
                int[][] boundaries = [new int[threadCount + 1], new int[threadCount + 1]];

                // At most 1 leftover nibble carried between batches
                Span<byte> leftover = stackalloc byte[2];
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

                        // Carry an odd trailing char into the next batch
                        var remainder = compactLen % 2;
                        if (!isLastBatch && remainder > 0)
                        {
                            leftover[0] = compactBufs[slot][--compactLen];
                            leftoverLen = 1;
                        }

                        var totalPairs = compactLen / 2;

                        // Phase 2: Parallel decode — each thread decodes complete hex pairs
                        var numChunks = 0;

                        if (totalPairs > 0)
                        {
                            numChunks = Math.Min(threadCount, totalPairs);
                            var gpc = (totalPairs + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 2);
                            }

                            var bounds = boundaries[slot];
                            bounds[0] = 0;
                            for (var i = 1; i <= numChunks; i++)
                                bounds[i] = Math.Min(i * gpc, totalPairs);

                            var sizes = chunkSizes[slot];
                            var cBuf = compactBufs[slot];
                            var ob = outputBufs[slot];

                            Parallel.For(0, numChunks, i =>
                            {
                                var startPair = bounds[i];
                                var endPair = bounds[i + 1];
                                var outPos = startPair;

                                for (var p = startPair; p < endPair; p++)
                                {
                                    var hi = ReverseTable[cBuf[p * 2]];
                                    var lo = ReverseTable[cBuf[p * 2 + 1]];
                                    ob[outPos++] = (byte)(hi << 4 | lo);
                                }

                                sizes[i] = outPos - startPair;
                            });
                        }

                        pending?.Wait();
                        pending = null;

                        if (totalPairs > 0)
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
            for (var i = 0; i < 6; i++)
            {
                table['a' + i] = 10 + i;
                table['A' + i] = 10 + i;
            }
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx2(byte* input, byte* output, int count)
        {
            var lut = HexLut256;
            var mask = MaskOf256;
            var processed = 0;
            while (processed + 32 <= count)
            {
                var raw = Avx.LoadVector256(input + processed);
                var lo = Avx2.And(raw, mask);
                var hi = Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 4).AsByte(), mask);

                var interleavedLo = Avx2.UnpackLow(hi, lo);
                var interleavedHi = Avx2.UnpackHigh(hi, lo);

                var hexLo = Avx2.Shuffle(lut, interleavedLo);
                var hexHi = Avx2.Shuffle(lut, interleavedHi);

                Avx.Store(output + processed * 2, Avx2.Permute2x128(hexLo, hexHi, 0x20));
                Avx.Store(output + processed * 2 + 32, Avx2.Permute2x128(hexLo, hexHi, 0x31));

                processed += 32;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx512(byte* input, byte* output, int count)
        {
            var lut = HexLut512;
            var mask = MaskOf512;
            var processed = 0;
            while (processed + 64 <= count)
            {
                var raw = Avx512F.LoadVector512(input + processed);
                var lo = Avx512F.And(raw, mask);
                var hi = Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 4).AsByte(), mask);

                var interleavedLo = Avx512BW.UnpackLow(hi, lo);
                var interleavedHi = Avx512BW.UnpackHigh(hi, lo);

                var hexLo = Avx512BW.Shuffle(lut, interleavedLo);
                var hexHi = Avx512BW.Shuffle(lut, interleavedHi);

                var hexLoLo = hexLo.GetLower();
                var hexLoHi = hexLo.GetUpper();
                var hexHiLo = hexHi.GetLower();
                var hexHiHi = hexHi.GetUpper();

                Avx.Store(output + processed * 2, Avx2.Permute2x128(hexLoLo, hexHiLo, 0x20));
                Avx.Store(output + processed * 2 + 32, Avx2.Permute2x128(hexLoLo, hexHiLo, 0x31));
                Avx.Store(output + processed * 2 + 64, Avx2.Permute2x128(hexLoHi, hexHiHi, 0x20));
                Avx.Store(output + processed * 2 + 96, Avx2.Permute2x128(hexLoHi, hexHiHi, 0x31));

                processed += 64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeScalar(byte[] input, int start, int count, byte[] output, int outStart)
        {
            for (var i = 0; i < count; i++)
            {
                var b = input[start + i];
                output[outStart + i * 2] = (byte)(b >> 4 < 10 ? '0' + (b >> 4) : 'a' + (b >> 4) - 10);
                output[outStart + i * 2 + 1] = (byte)((b & 0xf) < 10 ? '0' + (b & 0xf) : 'a' + (b & 0xf) - 10);
            }
        }
    }
}
