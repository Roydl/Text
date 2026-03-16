namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.Intrinsics;
    using System.Runtime.Intrinsics.X86;
    using System.Text;
    using System.Threading.Tasks;
    using Internal;
    using Resources;

    /// <summary>Provides functionality for encoding data into the Base-85 (also called Ascii85) text representation and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Base85 encodes data in independent 4-byte groups, forming no serial dependency chain between groups. This makes it highly amenable to SIMD vectorization (AVX2) and parallel processing across multiple cores, resulting in throughput that scales with available hardware.</remarks>
    public sealed class Base85 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1024 * 1024;
        private const uint MagicOf85 = 3233857729u;

        private static readonly Vector256<byte> Bswap32Mask =
            Vector256.Create((byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12, 3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

        private static readonly Vector256<byte> TransposeMask =
            Vector256.Create((byte)0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15);

        private static readonly Vector256<uint> Vec256Of33 = Vector256.Create(33u);
        private static readonly Vector256<uint> Vec256Of85 = Vector256.Create(85u);
        private static readonly Vector256<uint> MagicVec = Vector256.Create(MagicOf85);
        private static readonly uint[] HiDecodeTable = BuildDecodeTable(614125u);
        private static readonly uint[] MidDecodeTable = BuildDecodeTable(85u);

        /// <summary>Initializes a new instance of the <see cref="Base85"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base85() { }

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
                var useAvx2 = Avx2.IsSupported;

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];
                byte[][] outputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize / 4 * 5 + threadCount),
                    ArrayPool<byte>.Shared.Rent(inputSize / 4 * 5 + threadCount)
                ];
                int[][] chunkSizes = [new int[threadCount], new int[threadCount]];

                try
                {
                    Task pending = null;
                    var cur = 0;
                    var linePos = 0;
                    var totalRead = ReadFully(bsi, inputBufs[cur]);

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
                            var isLastChunk = i == numChunks - 1;
                            var procLen = isLastChunk && isLastBatch ? inputLen : inputLen & ~3;
                            var fullLen = procLen & ~3;
                            var remaining = procLen - fullLen;

                            var outStart = i * (ChunkSize / 4) * 5;
                            var outPos = outStart;
                            var input = inputBufs[slot].AsSpan(inputStart, fullLen);
                            var ob = outputBufs[slot];
                            var offset = 0;

                            if (useAvx2)
                            {
                                Span<byte> trans = stackalloc byte[32];
                                Span<uint> d4Buf = stackalloc uint[8];
                                var zero = Vector256<uint>.Zero;

                                while (offset + 32 <= fullLen)
                                {
                                    var raw = Vector256.LoadUnsafe(ref input[offset]);
                                    var t = Avx2.Shuffle(raw, Bswap32Mask).AsUInt32();
                                    offset += 32;

                                    // Any zero group falls back to scalar to preserve the 'z' shortcut
                                    if (Avx2.MoveMask(Avx2.CompareEqual(t, zero).AsByte()) != 0)
                                    {
                                        var groupBase = offset - 32;
                                        for (var gi = 0; gi < 8; gi++)
                                        {
                                            var tv = BinaryPrimitives.ReadUInt32BigEndian(input[(groupBase + gi * 4)..]);
                                            if (tv == 0)
                                                ob[outPos++] = 0x7a;
                                            else
                                            {
                                                EncodeGroup(tv, ob, outPos);
                                                outPos += 5;
                                            }
                                        }
                                        continue;
                                    }

                                    // 5 digit vectors for all 8 groups simultaneously
                                    var div = Div85(t);
                                    var d4 = Avx2.Add(Avx2.Subtract(t, Avx2.MultiplyLow(div, Vec256Of85)), Vec256Of33);
                                    t = div;
                                    div = Div85(t);
                                    var d3 = Avx2.Add(Avx2.Subtract(t, Avx2.MultiplyLow(div, Vec256Of85)), Vec256Of33);
                                    t = div;
                                    div = Div85(t);
                                    var d2 = Avx2.Add(Avx2.Subtract(t, Avx2.MultiplyLow(div, Vec256Of85)), Vec256Of33);
                                    t = div;
                                    div = Div85(t);
                                    var d1 = Avx2.Add(Avx2.Subtract(t, Avx2.MultiplyLow(div, Vec256Of85)), Vec256Of33);
                                    t = div;
                                    div = Div85(t);
                                    var d0 = Avx2.Add(Avx2.Subtract(t, Avx2.MultiplyLow(div, Vec256Of85)), Vec256Of33);

                                    // Pack d0v..d3v to bytes, transpose 4×4 per lane via vpshufb
                                    var d01 = Avx2.PackUnsignedSaturate(d0.AsInt32(), d1.AsInt32());
                                    var d23 = Avx2.PackUnsignedSaturate(d2.AsInt32(), d3.AsInt32());
                                    Avx2.Shuffle(Avx2.PackUnsignedSaturate(d01.AsInt16(), d23.AsInt16()), TransposeMask)
                                        .StoreUnsafe(ref trans[0]);
                                    d4.StoreUnsafe(ref d4Buf[0]);

                                    // 8 × uint32 write + 8 × byte write = 16 ops for 40 output bytes
                                    Unsafe.WriteUnaligned(ref ob[outPos + 0], Unsafe.ReadUnaligned<uint>(ref trans[0]));
                                    ob[outPos + 4] = (byte)d4Buf[0];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 5], Unsafe.ReadUnaligned<uint>(ref trans[4]));
                                    ob[outPos + 9] = (byte)d4Buf[1];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 10], Unsafe.ReadUnaligned<uint>(ref trans[8]));
                                    ob[outPos + 14] = (byte)d4Buf[2];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 15], Unsafe.ReadUnaligned<uint>(ref trans[12]));
                                    ob[outPos + 19] = (byte)d4Buf[3];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 20], Unsafe.ReadUnaligned<uint>(ref trans[16]));
                                    ob[outPos + 24] = (byte)d4Buf[4];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 25], Unsafe.ReadUnaligned<uint>(ref trans[20]));
                                    ob[outPos + 29] = (byte)d4Buf[5];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 30], Unsafe.ReadUnaligned<uint>(ref trans[24]));
                                    ob[outPos + 34] = (byte)d4Buf[6];
                                    Unsafe.WriteUnaligned(ref ob[outPos + 35], Unsafe.ReadUnaligned<uint>(ref trans[28]));
                                    ob[outPos + 39] = (byte)d4Buf[7];
                                    outPos += 40;
                                }
                            }

                            // Scalar tail for remaining complete groups after the last AVX2 batch
                            while (offset + 4 <= fullLen)
                            {
                                var t = BinaryPrimitives.ReadUInt32BigEndian(input[offset..]);
                                offset += 4;
                                if (t == 0)
                                    ob[outPos++] = 0x7a;
                                else
                                {
                                    EncodeGroup(t, ob, outPos);
                                    outPos += 5;
                                }
                            }

                            // Partial block: 1-3 bytes, only on the last chunk of the last batch
                            if (remaining > 0)
                            {
                                Span<byte> tmp = stackalloc byte[4];
                                inputBufs[slot].AsSpan(inputStart + fullLen, remaining).CopyTo(tmp);
                                var pt = BinaryPrimitives.ReadUInt32BigEndian(tmp);
                                Span<byte> eb = stackalloc byte[5];
                                for (var k = 4; k >= 0; k--)
                                {
                                    var d = (uint)((ulong)pt * MagicOf85 >> 38);
                                    eb[k] = (byte)(pt - d * 85 + 33);
                                    pt = d;
                                }
                                for (var k = 0; k <= remaining; k++)
                                    ob[outPos++] = eb[k];
                            }

                            chunkSizes[slot][i] = outPos - outStart;
                        });

                        pending?.Wait();

                        var capturedLinePos = linePos;
                        pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                        {
                            var pos = capturedLinePos;
                            for (var j = 0; j < numChunks; j++)
                            {
                                var start = j * (ChunkSize / 4) * 5;
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

                        // Swap buffer slot and read the next batch — overlaps with the write task above
                        cur ^= 1;
                        totalRead = ReadFully(bsi, inputBufs[cur]);
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
                var inputSize = ChunkSize * threadCount;

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
                    var cur = 0;
                    Task pending = null;
                    var bytesRead = ReadFully(bsi, inputBufs[cur]);

                    while (bytesRead > 0 || leftoverLen > 0)
                    {
                        var isLastBatch = bytesRead < inputSize;
                        var slot = cur;

                        // Phase 1: Compact — prepend leftover, strip whitespace, validate
                        leftover[..leftoverLen].CopyTo(compactBufs[slot]);
                        var compactLen = leftoverLen;
                        leftoverLen = 0;

                        for (var i = 0; i < bytesRead; i++)
                        {
                            var b = inputBufs[slot][i];
                            if (IsSkippable(b))
                                continue;
                            if (Separator.Span.Contains(b))
                                continue;
                            if (b != 'z' && b is < (byte)'!' or > (byte)'u')
                                throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)b));
                            compactBufs[slot][compactLen++] = b;
                        }

                        // Phase 2: Scan for complete groups ('z' = 1 byte, normal = 5 bytes)
                        var totalGroups = 0;
                        var scanPos = 0;
                        while (scanPos < compactLen)
                        {
                            var step = compactBufs[slot][scanPos] == 'z' ? 1 : 5;
                            if (scanPos + step > compactLen)
                                break;
                            scanPos += step;
                            totalGroups++;
                        }

                        var partialStart = scanPos;
                        var partialLen = compactLen - partialStart;

                        switch (isLastBatch)
                        {
                            case false when partialLen > 0:
                                compactBufs[slot].AsSpan(partialStart, partialLen).CopyTo(leftover);
                                leftoverLen = partialLen;
                                partialLen = 0;
                                break;
                            case true when partialLen == 1:
                                throw new DecoderFallbackException(ExceptionMessages.LastBlockIsSingleByte);
                        }

                        // Phase 3: Parallel decode of all complete groups
                        var numChunks = 0;
                        var gpc = 0;

                        if (totalGroups > 0)
                        {
                            numChunks = Math.Min(threadCount, totalGroups);
                            gpc = (totalGroups + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks * 4;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 4);
                            }

                            var sizes = chunkSizes[slot];
                            var bounds = boundaries[slot];
                            {
                                var p = 0;
                                var g = 0;
                                var c = 0;
                                bounds[0] = 0;
                                while (p < partialStart)
                                {
                                    var step = compactBufs[slot][p] == 'z' ? 1 : 5;
                                    p += step;
                                    g++;
                                    if (g == (c + 1) * gpc && c < numChunks - 1)
                                        bounds[++c] = p;
                                }
                                for (var i = c + 1; i <= numChunks; i++)
                                    bounds[i] = partialStart;
                            }

                            var buf = outputBufs[slot];
                            var cBuf = compactBufs[slot];
                            var groups = gpc;

                            Parallel.For(0, numChunks, i =>
                            {
                                var start = bounds[i];
                                var end = bounds[i + 1];
                                var outPos = i * groups * 4;
                                var p = start;

                                while (p < end)
                                {
                                    if (cBuf[p] == 'z')
                                    {
                                        buf[outPos++] = 0;
                                        buf[outPos++] = 0;
                                        buf[outPos++] = 0;
                                        buf[outPos++] = 0;
                                        p++;
                                    }
                                    else
                                    {
                                        // Two-table decode: 2 lookups + 2 additions instead of 5 multiplies
                                        var hi = (cBuf[p] - 33) * 85 + (cBuf[p + 1] - 33);
                                        var lo = (cBuf[p + 2] - 33) * 85 + (cBuf[p + 3] - 33);
                                        var t = HiDecodeTable[hi] + MidDecodeTable[lo] + (uint)(cBuf[p + 4] - 33);
                                        p += 5;
                                        buf[outPos++] = (byte)(t >> 24);
                                        buf[outPos++] = (byte)(t >> 16);
                                        buf[outPos++] = (byte)(t >> 8);
                                        buf[outPos++] = (byte)t;
                                    }
                                }

                                sizes[i] = outPos - i * groups * 4;
                            });
                        }

                        pending?.Wait();
                        pending = null;

                        if (totalGroups > 0)
                        {
                            pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                            {
                                for (var i = 0; i < numChunks; i++)
                                    bso.Write(outputBufs[slot], i * gpc * 4, chunkSizes[slot][i]);
                            });
                        }

                        // Phase 4: Final partial block — drain pending first to preserve byte order
                        if (isLastBatch && partialLen >= 2)
                        {
                            pending?.Wait();
                            pending = null;
                            var n = partialLen - 1;
                            var t = 0u;
                            for (var k = 0; k < partialLen; k++)
                                t += (uint)((compactBufs[slot][partialStart + k] - 33) * Pow85(k));
                            t += Pow85(n);
                            for (var k = 0; k < n; k++)
                                bso.WriteByte((byte)(t >> 24 - k * 8));
                        }

                        if (bytesRead == 0)
                            break;

                        // Swap buffer slot and read the next batch — overlaps with the write task above
                        cur ^= 1;
                        bytesRead = ReadFully(bsi, inputBufs[cur]);
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

        private static uint[] BuildDecodeTable(uint multiplier)
        {
            var table = new uint[7225];
            for (var i = 0; i < 7225; i++)
                table[i] = (uint)i * multiplier;
            return table;
        }

        // floor(v / 85) for all 8 uint32 lanes via magic-number multiply.
        // _mm256_mul_epu32 only covers even lanes; odd lanes are shuffled to even, divided, blended back.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> Div85(Vector256<uint> v)
        {
            var evenDiv = Avx2.ShiftRightLogical(Avx2.Multiply(v, MagicVec), 38).AsUInt32();
            var vOdd = Avx2.Shuffle(v.AsInt32(), 0xb1).AsUInt32();
            var oddDiv = Avx2.ShiftRightLogical(Avx2.Multiply(vOdd, MagicVec), 38).AsUInt32();

            // 0xaa = 10101010b: odd positions from oddDiv, even from evenDiv
            return Avx2.Blend(evenDiv, Avx2.Shuffle(oddDiv.AsInt32(), 0xb1).AsUInt32(), 0xaa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeGroup(uint t, byte[] buf, int pos)
        {
            for (var k = 4; k >= 0; k--)
            {
                var div = (uint)((ulong)t * MagicOf85 >> 38);
                buf[pos + k] = (byte)(t - div * 85 + 33);
                t = div;
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var totalRead = 0;
            int read;
            while (totalRead < buffer.Length && (read = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
                totalRead += read;
            return totalRead;
        }

        // Pow85 for the partial-block path only — not used in any hot path
        private static uint Pow85(int exp) => exp switch
        {
            0 => 85u * 85u * 85u * 85u,
            1 => 85u * 85u * 85u,
            2 => 85u * 85u,
            3 => 85u,
            _ => 1u
        };
    }
}
