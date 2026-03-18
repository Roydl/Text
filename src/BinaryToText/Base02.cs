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

    /// <summary>Provides functionality for encoding data into Base-2 (binary) text representations and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Each byte expands to 8 binary chars via pure bit-shifts with no division, and the power-of-two output stride enables full SIMD vectorization without scatter overhead. Encoding uses AVX-512BW (64 bytes/iter) or AVX2 (32 bytes/iter) with parallel processing across all cores.</remarks>
    public sealed class Base02 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1024 * 1024;
        private static readonly Vector256<byte> Vec1Of256 = Vector256.Create((byte)1);
        private static readonly Vector512<byte> Vec1Of512 = Vector512.Create((byte)1);
        private static readonly Vector256<byte> Vec48Of256 = Vector256.Create((byte)48);
        private static readonly Vector512<byte> Vec48Of512 = Vector512.Create((byte)48);
        private static readonly int[] ReverseTable = BuildReverseTable();

        /// <summary>Initializes a new instance of the <see cref="Base02"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base02() { }

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
                    ArrayPool<byte>.Shared.Rent(inputSize * 8),
                    ArrayPool<byte>.Shared.Rent(inputSize * 8)
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
                            var outStart = i * ChunkSize * 8;
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
                                                EncodeAvx2(pIn + avx512Count, pOut + avx512Count * 8, avx2Count);
                                                EncodeScalar(input, inputStart + avx512Count + avx2Count, inputLen - avx512Count - avx2Count, ob, outStart + (avx512Count + avx2Count) * 8);
                                            }
                                            else
                                                EncodeScalar(input, inputStart + avx512Count, inputLen - avx512Count, ob, outStart + avx512Count * 8);
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
                                            EncodeScalar(input, inputStart + avx2Count, inputLen - avx2Count, ob, outStart + avx2Count * 8);
                                        }
                                }
                            }
                            else
                                EncodeScalar(input, inputStart, inputLen, ob, outStart);

                            chunkSizes[slot][i] = inputLen * 8;
                        });

                        pending?.Wait();

                        var capturedLinePos = linePos;
                        pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                        {
                            var pos = capturedLinePos;
                            for (var j = 0; j < numChunks; j++)
                            {
                                var start = j * ChunkSize * 8;
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
                var inputSize = ChunkSize * 8 * threadCount;

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];
                byte[][] compactBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize + 8),
                    ArrayPool<byte>.Shared.Rent(inputSize + 8)
                ];
                byte[][] outputBufs = [null, null];

                int[][] chunkSizes = [new int[threadCount], new int[threadCount]];
                int[][] boundaries = [new int[threadCount + 1], new int[threadCount + 1]];

                Span<byte> leftover = stackalloc byte[8];
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

                        // Carry incomplete octet into the next batch
                        var remainder = compactLen % 8;
                        if (!isLastBatch && remainder > 0)
                        {
                            compactBufs[slot].AsSpan(compactLen - remainder, remainder).CopyTo(leftover);
                            leftoverLen = remainder;
                            compactLen -= remainder;
                        }

                        var totalOctets = compactLen / 8;
                        var numChunks = 0;

                        if (totalOctets > 0)
                        {
                            numChunks = Math.Min(threadCount, totalOctets);
                            var gpc = (totalOctets + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 8);
                            }

                            var bounds = boundaries[slot];
                            bounds[0] = 0;
                            for (var i = 1; i <= numChunks; i++)
                                bounds[i] = Math.Min(i * gpc, totalOctets);

                            var sizes = chunkSizes[slot];
                            var cBuf = compactBufs[slot];
                            var ob = outputBufs[slot];

                            Parallel.For(0, numChunks, i =>
                            {
                                var startOctet = bounds[i];
                                var endOctet = bounds[i + 1];
                                var outPos = startOctet;

                                for (var p = startOctet; p < endOctet; p++)
                                {
                                    var o = p * 8;
                                    ob[outPos++] = (byte)(
                                        ReverseTable[cBuf[o]] << 7 |
                                        ReverseTable[cBuf[o + 1]] << 6 |
                                        ReverseTable[cBuf[o + 2]] << 5 |
                                        ReverseTable[cBuf[o + 3]] << 4 |
                                        ReverseTable[cBuf[o + 4]] << 3 |
                                        ReverseTable[cBuf[o + 5]] << 2 |
                                        ReverseTable[cBuf[o + 6]] << 1 |
                                        ReverseTable[cBuf[o + 7]]);
                                }

                                sizes[i] = outPos - startOctet;
                            });
                        }

                        pending?.Wait();
                        pending = null;

                        if (totalOctets > 0)
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
            table['0'] = 0;
            table['1'] = 1;
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx512(byte* input, byte* output, int count)
        {
            var v48 = Vec48Of512;
            var v1 = Vec1Of512;
            var processed = 0;

            while (processed + 64 <= count)
            {
                var raw = Avx512F.LoadVector512(input + processed);

                var b7 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 7).AsByte(), v1), v48);
                var b6 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 6).AsByte(), v1), v48);
                var b5 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 5).AsByte(), v1), v48);
                var b4 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 4).AsByte(), v1), v48);
                var b3 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 3).AsByte(), v1), v48);
                var b2 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 2).AsByte(), v1), v48);
                var b1 = Avx512BW.Add(Avx512F.And(Avx512BW.ShiftRightLogical(raw.AsUInt16(), 1).AsByte(), v1), v48);
                var b0 = Avx512BW.Add(Avx512F.And(raw, v1), v48);

                var p76Lo = Avx512BW.UnpackLow(b7, b6);
                var p76Hi = Avx512BW.UnpackHigh(b7, b6);
                var p54Lo = Avx512BW.UnpackLow(b5, b4);
                var p54Hi = Avx512BW.UnpackHigh(b5, b4);
                var p32Lo = Avx512BW.UnpackLow(b3, b2);
                var p32Hi = Avx512BW.UnpackHigh(b3, b2);
                var p10Lo = Avx512BW.UnpackLow(b1, b0);
                var p10Hi = Avx512BW.UnpackHigh(b1, b0);

                var q7654Lo = Avx512BW.UnpackLow(p76Lo.AsUInt16(), p54Lo.AsUInt16()).AsByte();
                var q7654Hi = Avx512BW.UnpackHigh(p76Lo.AsUInt16(), p54Lo.AsUInt16()).AsByte();
                var q3210Lo = Avx512BW.UnpackLow(p32Lo.AsUInt16(), p10Lo.AsUInt16()).AsByte();
                var q3210Hi = Avx512BW.UnpackHigh(p32Lo.AsUInt16(), p10Lo.AsUInt16()).AsByte();
                var q7654Lo2 = Avx512BW.UnpackLow(p76Hi.AsUInt16(), p54Hi.AsUInt16()).AsByte();
                var q7654Hi2 = Avx512BW.UnpackHigh(p76Hi.AsUInt16(), p54Hi.AsUInt16()).AsByte();
                var q3210Lo2 = Avx512BW.UnpackLow(p32Hi.AsUInt16(), p10Hi.AsUInt16()).AsByte();
                var q3210Hi2 = Avx512BW.UnpackHigh(p32Hi.AsUInt16(), p10Hi.AsUInt16()).AsByte();

                var r0 = Avx512F.UnpackLow(q7654Lo.AsUInt32(), q3210Lo.AsUInt32()).AsByte();
                var r1 = Avx512F.UnpackHigh(q7654Lo.AsUInt32(), q3210Lo.AsUInt32()).AsByte();
                var r2 = Avx512F.UnpackLow(q7654Hi.AsUInt32(), q3210Hi.AsUInt32()).AsByte();
                var r3 = Avx512F.UnpackHigh(q7654Hi.AsUInt32(), q3210Hi.AsUInt32()).AsByte();
                var r4 = Avx512F.UnpackLow(q7654Lo2.AsUInt32(), q3210Lo2.AsUInt32()).AsByte();
                var r5 = Avx512F.UnpackHigh(q7654Lo2.AsUInt32(), q3210Lo2.AsUInt32()).AsByte();
                var r6 = Avx512F.UnpackLow(q7654Hi2.AsUInt32(), q3210Hi2.AsUInt32()).AsByte();
                var r7 = Avx512F.UnpackHigh(q7654Hi2.AsUInt32(), q3210Hi2.AsUInt32()).AsByte();

                var r0Lo = Avx512F.ExtractVector256(r0, 0);
                var r0Hi = Avx512F.ExtractVector256(r0, 1);
                var r1Lo = Avx512F.ExtractVector256(r1, 0);
                var r1Hi = Avx512F.ExtractVector256(r1, 1);
                var r2Lo = Avx512F.ExtractVector256(r2, 0);
                var r2Hi = Avx512F.ExtractVector256(r2, 1);
                var r3Lo = Avx512F.ExtractVector256(r3, 0);
                var r3Hi = Avx512F.ExtractVector256(r3, 1);
                var r4Lo = Avx512F.ExtractVector256(r4, 0);
                var r4Hi = Avx512F.ExtractVector256(r4, 1);
                var r5Lo = Avx512F.ExtractVector256(r5, 0);
                var r5Hi = Avx512F.ExtractVector256(r5, 1);
                var r6Lo = Avx512F.ExtractVector256(r6, 0);
                var r6Hi = Avx512F.ExtractVector256(r6, 1);
                var r7Lo = Avx512F.ExtractVector256(r7, 0);
                var r7Hi = Avx512F.ExtractVector256(r7, 1);

                var outBase = processed * 8;

                Avx.Store(output + outBase, Avx2.Permute2x128(r0Lo, r1Lo, 0x20));
                Avx.Store(output + outBase + 32, Avx2.Permute2x128(r2Lo, r3Lo, 0x20));
                Avx.Store(output + outBase + 64, Avx2.Permute2x128(r4Lo, r5Lo, 0x20));
                Avx.Store(output + outBase + 96, Avx2.Permute2x128(r6Lo, r7Lo, 0x20));
                Avx.Store(output + outBase + 128, Avx2.Permute2x128(r0Lo, r1Lo, 0x31));
                Avx.Store(output + outBase + 160, Avx2.Permute2x128(r2Lo, r3Lo, 0x31));
                Avx.Store(output + outBase + 192, Avx2.Permute2x128(r4Lo, r5Lo, 0x31));
                Avx.Store(output + outBase + 224, Avx2.Permute2x128(r6Lo, r7Lo, 0x31));

                Avx.Store(output + outBase + 256, Avx2.Permute2x128(r0Hi, r1Hi, 0x20));
                Avx.Store(output + outBase + 288, Avx2.Permute2x128(r2Hi, r3Hi, 0x20));
                Avx.Store(output + outBase + 320, Avx2.Permute2x128(r4Hi, r5Hi, 0x20));
                Avx.Store(output + outBase + 352, Avx2.Permute2x128(r6Hi, r7Hi, 0x20));
                Avx.Store(output + outBase + 384, Avx2.Permute2x128(r0Hi, r1Hi, 0x31));
                Avx.Store(output + outBase + 416, Avx2.Permute2x128(r2Hi, r3Hi, 0x31));
                Avx.Store(output + outBase + 448, Avx2.Permute2x128(r4Hi, r5Hi, 0x31));
                Avx.Store(output + outBase + 480, Avx2.Permute2x128(r6Hi, r7Hi, 0x31));

                processed += 64;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx2(byte* input, byte* output, int count)
        {
            var v48 = Vec48Of256;
            var v1 = Vec1Of256;
            var processed = 0;

            while (processed + 32 <= count)
            {
                var raw = Avx.LoadVector256(input + processed);

                var b7 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 7).AsByte(), v1), v48);
                var b6 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 6).AsByte(), v1), v48);
                var b5 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 5).AsByte(), v1), v48);
                var b4 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 4).AsByte(), v1), v48);
                var b3 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 3).AsByte(), v1), v48);
                var b2 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 2).AsByte(), v1), v48);
                var b1 = Avx2.Add(Avx2.And(Avx2.ShiftRightLogical(raw.AsUInt16(), 1).AsByte(), v1), v48);
                var b0 = Avx2.Add(Avx2.And(raw, v1), v48);

                var p76Lo = Avx2.UnpackLow(b7, b6);
                var p76Hi = Avx2.UnpackHigh(b7, b6);
                var p54Lo = Avx2.UnpackLow(b5, b4);
                var p54Hi = Avx2.UnpackHigh(b5, b4);
                var p32Lo = Avx2.UnpackLow(b3, b2);
                var p32Hi = Avx2.UnpackHigh(b3, b2);
                var p10Lo = Avx2.UnpackLow(b1, b0);
                var p10Hi = Avx2.UnpackHigh(b1, b0);

                var q7654Lo = Avx2.UnpackLow(p76Lo.AsUInt16(), p54Lo.AsUInt16()).AsByte();
                var q7654Hi = Avx2.UnpackHigh(p76Lo.AsUInt16(), p54Lo.AsUInt16()).AsByte();
                var q3210Lo = Avx2.UnpackLow(p32Lo.AsUInt16(), p10Lo.AsUInt16()).AsByte();
                var q3210Hi = Avx2.UnpackHigh(p32Lo.AsUInt16(), p10Lo.AsUInt16()).AsByte();
                var q7654Lo2 = Avx2.UnpackLow(p76Hi.AsUInt16(), p54Hi.AsUInt16()).AsByte();
                var q7654Hi2 = Avx2.UnpackHigh(p76Hi.AsUInt16(), p54Hi.AsUInt16()).AsByte();
                var q3210Lo2 = Avx2.UnpackLow(p32Hi.AsUInt16(), p10Hi.AsUInt16()).AsByte();
                var q3210Hi2 = Avx2.UnpackHigh(p32Hi.AsUInt16(), p10Hi.AsUInt16()).AsByte();

                var r0 = Avx2.UnpackLow(q7654Lo.AsUInt32(), q3210Lo.AsUInt32()).AsByte();
                var r1 = Avx2.UnpackHigh(q7654Lo.AsUInt32(), q3210Lo.AsUInt32()).AsByte();
                var r2 = Avx2.UnpackLow(q7654Hi.AsUInt32(), q3210Hi.AsUInt32()).AsByte();
                var r3 = Avx2.UnpackHigh(q7654Hi.AsUInt32(), q3210Hi.AsUInt32()).AsByte();
                var r4 = Avx2.UnpackLow(q7654Lo2.AsUInt32(), q3210Lo2.AsUInt32()).AsByte();
                var r5 = Avx2.UnpackHigh(q7654Lo2.AsUInt32(), q3210Lo2.AsUInt32()).AsByte();
                var r6 = Avx2.UnpackLow(q7654Hi2.AsUInt32(), q3210Hi2.AsUInt32()).AsByte();
                var r7 = Avx2.UnpackHigh(q7654Hi2.AsUInt32(), q3210Hi2.AsUInt32()).AsByte();

                Avx.Store(output + processed * 8, Avx2.Permute2x128(r0, r1, 0x20));
                Avx.Store(output + processed * 8 + 32, Avx2.Permute2x128(r2, r3, 0x20));
                Avx.Store(output + processed * 8 + 64, Avx2.Permute2x128(r4, r5, 0x20));
                Avx.Store(output + processed * 8 + 96, Avx2.Permute2x128(r6, r7, 0x20));
                Avx.Store(output + processed * 8 + 128, Avx2.Permute2x128(r0, r1, 0x31));
                Avx.Store(output + processed * 8 + 160, Avx2.Permute2x128(r2, r3, 0x31));
                Avx.Store(output + processed * 8 + 192, Avx2.Permute2x128(r4, r5, 0x31));
                Avx.Store(output + processed * 8 + 224, Avx2.Permute2x128(r6, r7, 0x31));

                processed += 32;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeScalar(byte[] input, int start, int count, byte[] output, int outStart)
        {
            for (var i = 0; i < count; i++)
            {
                var b = input[start + i];
                output[outStart + i * 8] = (byte)('0' + (b >> 7 & 1));
                output[outStart + i * 8 + 1] = (byte)('0' + (b >> 6 & 1));
                output[outStart + i * 8 + 2] = (byte)('0' + (b >> 5 & 1));
                output[outStart + i * 8 + 3] = (byte)('0' + (b >> 4 & 1));
                output[outStart + i * 8 + 4] = (byte)('0' + (b >> 3 & 1));
                output[outStart + i * 8 + 5] = (byte)('0' + (b >> 2 & 1));
                output[outStart + i * 8 + 6] = (byte)('0' + (b >> 1 & 1));
                output[outStart + i * 8 + 7] = (byte)('0' + (b & 1));
            }
        }
    }
}
