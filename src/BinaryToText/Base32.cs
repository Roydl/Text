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

    /// <summary>Provides functionality for encoding data into the Base-32 text representations and back.</summary>
    /// <remarks><b>Performance:</b> Moderately optimized. Although Base32 encodes data in independent 5-byte groups with no serial dependency chain, the non-power-of-two group width makes full SIMD vectorization impractical. AVX2 is used for the alphabet lookup phase, while the 5-bit extraction remains scalar. Parallelization across multiple cores provides the majority of the throughput gain. Approximately 7x slower than Base64 and 6x slower than Base16.</remarks>
    public sealed class Base32 : BinaryToTextEncoding
    {
        private const int ChunkSize = 1000 * 1024;

        /// <summary>Standard 32-character set:
        ///     <para><code>ABCDEFGHIJKLMNOPQRSTUVWXYZ234567</code></para>
        /// </summary>
        private static readonly byte[] DefCharacterTable32 =
        [
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
            0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50,
            0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
            0x59, 0x5a, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37
        ];

        private static readonly int[] ReverseTable32 = BuildReverseTable();

        private static readonly Vector256<byte> AlphaHi =
            Avx2.IsSupported
                ? Avx2.Permute2x128(Vector256.Create(DefCharacterTable32),
                                    Vector256.Create(DefCharacterTable32), 0x11)
                : Vector256<byte>.Zero;

        private static readonly Vector256<byte> AlphaLo =
            Avx2.IsSupported
                ? Avx2.Permute2x128(Vector256.Create(DefCharacterTable32),
                                    Vector256.Create(DefCharacterTable32), 0x00)
                : Vector256<byte>.Zero;

        private static readonly Vector256<sbyte> SByte15 = Vector256.Create((sbyte)15);
        private static readonly Vector256<byte> Vec16 = Vector256.Create((byte)16);

        /// <summary>Initializes a new instance of the <see cref="Base32"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base32() { }

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
                    ArrayPool<byte>.Shared.Rent(inputSize / 5 * 8 + threadCount * 8),
                    ArrayPool<byte>.Shared.Rent(inputSize / 5 * 8 + threadCount * 8)
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
                            var isLastChunk = i == numChunks - 1;
                            var isFinal = isLastChunk && isLastBatch;
                            var procLen = isFinal ? inputLen : inputLen / 5 * 5;
                            var fullGroups = procLen / 5;
                            var rem = procLen - fullGroups * 5;

                            var outStart = i * (ChunkSize / 5 * 8 + 8);
                            var outPos = outStart;
                            var ob = outputBufs[slot];
                            var input = inputBufs[slot];

                            if (useAvx2 && fullGroups >= 4)
                            {
                                unsafe
                                {
                                    fixed (byte* pIn = &input[inputStart])
                                        fixed (byte* pOut = &ob[outStart])
                                        {
                                            var avx2Groups = fullGroups / 4 * 4;
                                            EncodeAvx2(pIn, pOut, avx2Groups);
                                            outPos = outStart + avx2Groups * 8;

                                            for (var g = avx2Groups; g < fullGroups; g++)
                                            {
                                                var s = inputStart + g * 5;
                                                EncodeGroup(input[s], input[s + 1], input[s + 2],
                                                            input[s + 3], input[s + 4], ob, outPos);
                                                outPos += 8;
                                            }
                                        }
                                }
                            }
                            else
                            {
                                for (var g = 0; g < fullGroups; g++)
                                {
                                    var s = inputStart + g * 5;
                                    EncodeGroup(input[s], input[s + 1], input[s + 2],
                                                input[s + 3], input[s + 4], ob, outPos);
                                    outPos += 8;
                                }
                            }

                            // Partial block: 1-4 bytes, only on the last chunk of the last batch
                            if (isFinal && rem > 0)
                            {
                                var s = inputStart + fullGroups * 5;
                                var bits = rem * 8;
                                var numChars = (bits + 4) / 5;
                                var buf = 0ul;
                                for (var k = 0; k < rem; k++)
                                    buf = buf << 8 | input[s + k];
                                buf <<= numChars * 5 - bits;
                                for (var k = 0; k < numChars; k++)
                                    ob[outPos++] = DefCharacterTable32[buf >> (numChars - 1 - k) * 5 & 0x1f];
                                for (var k = numChars; k < 8; k++)
                                    ob[outPos++] = (byte)'=';
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
                                var start = j * (ChunkSize / 5 * 8 + 8);
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
                var inputSize = ChunkSize / 5 * 8 * threadCount;

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

                        leftover[..leftoverLen].CopyTo(compactBufs[slot]);
                        var compactLen = leftoverLen;
                        leftoverLen = 0;

                        for (var i = 0; i < bytesRead; i++)
                        {
                            var b = inputBufs[slot][i];
                            if (IsSkippable(b) || Separator.Span.Contains(b) || b == '=')
                                continue;
                            if (ReverseTable32[b] == -1)
                                throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)b));
                            compactBufs[slot][compactLen++] = b;
                        }

                        var remainder = compactLen % 8;
                        if (!isLastBatch && remainder > 0)
                        {
                            compactBufs[slot].AsSpan(compactLen - remainder, remainder).CopyTo(leftover);
                            leftoverLen = remainder;
                            compactLen -= remainder;
                        }

                        var partialLen = isLastBatch ? compactLen % 8 : 0;
                        var totalGroups = compactLen / 8;

                        if (isLastBatch && partialLen is 1 or 3 or 6)
                            throw new DecoderFallbackException(ExceptionMessages.LastBlockIsSingleByte);

                        var numChunks = 0;

                        if (totalGroups > 0)
                        {
                            numChunks = Math.Min(threadCount, totalGroups);
                            var gpc = (totalGroups + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks * 5;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 8);
                            }

                            var bounds = boundaries[slot];
                            bounds[0] = 0;
                            for (var i = 1; i <= numChunks; i++)
                                bounds[i] = Math.Min(i * gpc, totalGroups);

                            var sizes = chunkSizes[slot];
                            var cBuf = compactBufs[slot];
                            var ob = outputBufs[slot];

                            Parallel.For(0, numChunks, i =>
                            {
                                var startGroup = bounds[i];
                                var endGroup = bounds[i + 1];
                                var outPos = startGroup * 5;

                                for (var g = startGroup; g < endGroup; g++)
                                {
                                    var p = g * 8;
                                    var v0 = (uint)ReverseTable32[cBuf[p]];
                                    var v1 = (uint)ReverseTable32[cBuf[p + 1]];
                                    var v2 = (uint)ReverseTable32[cBuf[p + 2]];
                                    var v3 = (uint)ReverseTable32[cBuf[p + 3]];
                                    var v4 = (uint)ReverseTable32[cBuf[p + 4]];
                                    var v5 = (uint)ReverseTable32[cBuf[p + 5]];
                                    var v6 = (uint)ReverseTable32[cBuf[p + 6]];
                                    var v7 = (uint)ReverseTable32[cBuf[p + 7]];
                                    ob[outPos++] = (byte)(v0 << 3 | v1 >> 2);
                                    ob[outPos++] = (byte)(v1 << 6 | v2 << 1 | v3 >> 4);
                                    ob[outPos++] = (byte)(v3 << 4 | v4 >> 1);
                                    ob[outPos++] = (byte)(v4 << 7 | v5 << 2 | v6 >> 3);
                                    ob[outPos++] = (byte)(v6 << 5 | v7);
                                }

                                sizes[i] = outPos - startGroup * 5;
                            });
                        }

                        pending?.Wait();
                        pending = null;

                        if (totalGroups > 0)
                        {
                            var num = numChunks;
                            var bounds = boundaries[slot];
                            pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                            {
                                for (var i = 0; i < num; i++)
                                    bso.Write(outputBufs[slot], bounds[i] * 5, chunkSizes[slot][i]);
                            });
                        }

                        if (isLastBatch && partialLen > 0)
                        {
                            pending?.Wait();
                            pending = null;
                            var p = totalGroups * 8;
                            var buf = 0ul;
                            for (var k = 0; k < partialLen; k++)
                                buf = buf << 5 | (uint)ReverseTable32[compactBufs[slot][p + k]];
                            var totalBits = partialLen * 5;
                            var outBytes = totalBits / 8;
                            buf >>= totalBits % 8;
                            for (var k = outBytes - 1; k >= 0; k--)
                                bso.WriteByte((byte)(buf >> k * 8 & 0xff));
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
            for (var i = 0; i < 26; i++)
                table['A' + i] = i;
            for (var i = 0; i < 6; i++)
                table['2' + i] = 26 + i;
            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EncodeGroup(byte b0, byte b1, byte b2, byte b3, byte b4, byte[] ob, int pos)
        {
            ob[pos + 0] = DefCharacterTable32[b0 >> 3];
            ob[pos + 1] = DefCharacterTable32[(b0 & 0x07) << 2 | b1 >> 6];
            ob[pos + 2] = DefCharacterTable32[b1 >> 1 & 0x1f];
            ob[pos + 3] = DefCharacterTable32[(b1 & 0x01) << 4 | b2 >> 4];
            ob[pos + 4] = DefCharacterTable32[(b2 & 0x0f) << 1 | b3 >> 7];
            ob[pos + 5] = DefCharacterTable32[b3 >> 2 & 0x1f];
            ob[pos + 6] = DefCharacterTable32[(b3 & 0x03) << 3 | b4 >> 5];
            ob[pos + 7] = DefCharacterTable32[b4 & 0x1f];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static void EncodeAvx2(byte* input, byte* output, int groups)
        {
            var processed = 0;
            Span<byte> idx = stackalloc byte[32];
            while (processed + 4 <= groups)
            {
                for (var g = 0; g < 4; g++)
                {
                    var b = input + (processed + g) * 5;
                    var v = (ulong)b[0] << 32 | (ulong)b[1] << 24 |
                            (ulong)b[2] << 16 | (ulong)b[3] << 8 | b[4];
                    idx[g * 8 + 0] = (byte)(v >> 35 & 0x1f);
                    idx[g * 8 + 1] = (byte)(v >> 30 & 0x1f);
                    idx[g * 8 + 2] = (byte)(v >> 25 & 0x1f);
                    idx[g * 8 + 3] = (byte)(v >> 20 & 0x1f);
                    idx[g * 8 + 4] = (byte)(v >> 15 & 0x1f);
                    idx[g * 8 + 5] = (byte)(v >> 10 & 0x1f);
                    idx[g * 8 + 6] = (byte)(v >> 5 & 0x1f);
                    idx[g * 8 + 7] = (byte)(v & 0x1f);
                }

                var idxVec = Vector256.LoadUnsafe(ref idx[0]);
                var gt15 = Avx2.CompareGreaterThan(idxVec.AsSByte(), SByte15).AsByte();
                Avx.Store(output + processed * 8, Avx2.BlendVariable(Avx2.Shuffle(AlphaLo, idxVec), Avx2.Shuffle(AlphaHi, Avx2.Subtract(idxVec, Vec16)), gt15));

                processed += 4;
            }

            // Scalar tail for remaining complete groups after the last AVX2 batch
            for (var g = processed; g < groups; g++)
            {
                var b = input + g * 5;
                var v = (ulong)b[0] << 32 | (ulong)b[1] << 24 | (ulong)b[2] << 16 | (ulong)b[3] << 8 | b[4];
                var o = output + g * 8;
                o[0] = DefCharacterTable32[v >> 35 & 0x1f];
                o[1] = DefCharacterTable32[v >> 30 & 0x1f];
                o[2] = DefCharacterTable32[v >> 25 & 0x1f];
                o[3] = DefCharacterTable32[v >> 20 & 0x1f];
                o[4] = DefCharacterTable32[v >> 15 & 0x1f];
                o[5] = DefCharacterTable32[v >> 10 & 0x1f];
                o[6] = DefCharacterTable32[v >> 5 & 0x1f];
                o[7] = DefCharacterTable32[v & 0x1f];
            }
        }
    }
}
