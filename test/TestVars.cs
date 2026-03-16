namespace Roydl.Text.Test
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public enum TestVarsType
    {
        TestStream,
        TestBytes,
        TestString,
        TestFile,
        QuoteString,
        RangeString,
        RandomBytes
    }

    public enum TestBytePattern
    {
        Random,
        AllZeros, // max 'z' group density → best case for encoders
        Sequential, // no 'z', no cache warmup effect, real mix
        Mixed // 25% zeros, 75% random → approximates real binary data
    }

    public static class TestVars
    {
        public const string PlatformCross = "Win32NT,Linux";
        public const string QuoteStr = "We know what we are, but know not what we may be.";
        public const string TestStr = "Test";
        public static readonly byte[] TestBytes = "Test"u8.ToArray();

        public static string RangeStr { get; } = new(Enumerable.Range(byte.MinValue, byte.MaxValue).Select(i => (char)i).ToArray());

        public static Random Randomizer => new();

        public static Stopwatch StopWatch => new();

        public static byte[] GetRandomBytes(int size = 0, TestBytePattern pattern = TestBytePattern.Random)
        {
            if (size < 1)
            {
                // Odd sizes stress the partial-block path
                size = Randomizer.Next(byte.MaxValue, short.MaxValue);
                if (size % 2 == 0)
                    --size;
            }
            var bytes = new byte[size];
            switch (pattern)
            {
                case TestBytePattern.AllZeros:
                    // Already zero-initialized by the runtime — nothing to do
                    break;
                case TestBytePattern.Sequential:
                    for (var i = 0; i < bytes.Length; i++)
                        bytes[i] = (byte)i;
                    break;
                case TestBytePattern.Mixed:
                    Randomizer.NextBytes(bytes);

                    // ~6% zero groups (1 in 16) — approximates real binary data such as PDFs/executables
                    for (var i = 0; i + 3 < bytes.Length; i += 64)
                    {
                        bytes[i] = 0;
                        bytes[i + 1] = 0;
                        bytes[i + 2] = 0;
                        bytes[i + 3] = 0;
                    }
                    break;
                default:
                    Randomizer.NextBytes(bytes);
                    break;
            }
            return bytes;
        }

        public static string GetTempFilePath(string name)
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            return Path.Combine(dir, $"test-{name}-{Guid.NewGuid()}.tmp");
        }
    }
}
