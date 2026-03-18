namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Buffers;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Internal;
    using Resources;
    using NetBase64 = System.Buffers.Text.Base64;

    /// <summary>Provides functionality for encoding data into the Base-64 text representations and back.</summary>
    /// <remarks><b>Performance:</b> Highly optimized. Base64 encodes data in independent 3-byte groups with no serial dependency chain, making it amenable to SIMD vectorization and parallel processing. Encoding and decoding leverage .NET's hardware-accelerated <see cref="System.Buffers.Text.Base64"/> implementation (AVX2 on supported hardware) across multiple cores.</remarks>
    public sealed class Base64 : BinaryToTextEncoding
    {
        private const int ChunkSize = 999 * 1024;
        private static readonly int[] ReverseTable = BuildReverseTable();

        /// <summary>Initializes a new instance of the <see cref="Base64"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base64() { }

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

                byte[][] inputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize),
                    ArrayPool<byte>.Shared.Rent(inputSize)
                ];
                byte[][] outputBufs =
                [
                    ArrayPool<byte>.Shared.Rent(inputSize / 3 * 4 + threadCount * 4),
                    ArrayPool<byte>.Shared.Rent(inputSize / 3 * 4 + threadCount * 4)
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

                            // Non-final chunks must be a multiple of 3 — no padding emitted
                            var procLen = isFinal ? inputLen : inputLen / 3 * 3;

                            // Each chunk slot
                            var outStart = i * (ChunkSize / 3 * 4 + 4);

                            NetBase64.EncodeToUtf8(inputBufs[slot].AsSpan(inputStart, procLen),
                                                   outputBufs[slot].AsSpan(outStart),
                                                   out _,
                                                   out var written,
                                                   isFinal);

                            chunkSizes[slot][i] = written;
                        });

                        pending?.Wait();

                        var capturedLinePos = linePos;
                        pending = Task.Run([SuppressMessage("ReSharper", "AccessToDisposedClosure")]() =>
                        {
                            var pos = capturedLinePos;
                            for (var j = 0; j < numChunks; j++)
                            {
                                var start = j * (ChunkSize / 3 * 4 + 4);
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

                        // Swap buffer slot and read next batch — overlaps with write task above
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
                var inputSize = ChunkSize / 3 * 4 * threadCount;

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

                // At most 3 chars of an incomplete Base64 group carried between batches
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

                        // Phase 1: Compact — prepend leftover, strip whitespace and separators, validate
                        leftover[..leftoverLen].CopyTo(compactBufs[slot]);
                        var compactLen = leftoverLen;
                        leftoverLen = 0;

                        for (var i = 0; i < bytesRead; i++)
                        {
                            var b = inputBufs[slot][i];
                            if (IsSkippable(b) || Separator.Span.Contains(b))
                                continue;
                            if (b != '=' && ReverseTable[b] == -1)
                                throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)b));
                            compactBufs[slot][compactLen++] = b;
                        }

                        // Carry any incomplete group (< 4 chars) into the next batch
                        var remainder = compactLen % 4;
                        if (!isLastBatch && remainder > 0)
                        {
                            compactBufs[slot].AsSpan(compactLen - remainder, remainder).CopyTo(leftover);
                            leftoverLen = remainder;
                            compactLen -= remainder;
                        }

                        // Phase 2: Parallel decode — each thread processes complete 4-char groups
                        var totalGroups = compactLen / 4;
                        var numChunks = 0;

                        if (totalGroups > 0)
                        {
                            numChunks = Math.Min(threadCount, totalGroups);
                            var gpc = (totalGroups + numChunks - 1) / numChunks;
                            var outSize = gpc * numChunks * 3;

                            if (outputBufs[slot] == null || outputBufs[slot].Length < outSize)
                            {
                                if (outputBufs[slot] != null)
                                    ArrayPool<byte>.Shared.Return(outputBufs[slot]);
                                outputBufs[slot] = ArrayPool<byte>.Shared.Rent(outSize + 4);
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
                                var isFinal = i == numChunks - 1 && isLastBatch;

                                NetBase64.DecodeFromUtf8(cBuf.AsSpan(startGroup * 4, (endGroup - startGroup) * 4),
                                                         ob.AsSpan(startGroup * 3),
                                                         out _,
                                                         out var written,
                                                         isFinal);

                                sizes[i] = written;
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
                                    bso.Write(outputBufs[slot], bounds[i] * 3, chunkSizes[slot][i]);
                            });
                        }

                        if (bytesRead == 0)
                            break;

                        // Swap buffer slot and read next batch — overlaps with write task above
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
            {
                table['A' + i] = i;
                table['a' + i] = i + 26;
            }
            for (var i = 0; i < 10; i++)
                table['0' + i] = i + 52;
            table['+'] = 62;
            table['/'] = 63;
            return table;
        }
    }
}
