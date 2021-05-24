namespace Roydl.Text.Test
{
    using System;
    using System.IO;
    using System.Linq;

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

    public static class TestVars
    {
        public const string PlatformInclude = "Win32NT,Linux";
        public const string QuoteStr = "We know what we are, but know not what we may be.";
        public const string TestStr = "Test";
        public static readonly byte[] TestBytes = { 0x54, 0x65, 0x73, 0x74 };
        private static readonly Random Randomizer = new();

        public static string RangeStr { get; } = new(Enumerable.Range(byte.MinValue, byte.MaxValue).Select(i => (char)i).ToArray());

        public static byte[] GetRandomBytes(int size = 0)
        {
            if (size < 1)
            {
                size = Randomizer.Next(byte.MaxValue, short.MaxValue);
                if (size % 2 == 0)
                    --size;
            }
            var bytes = new byte[size];
            Randomizer.NextBytes(bytes);
            return bytes;
        }

        public static string GetTempFilePath(string name)
        {
            var dir = Environment.CurrentDirectory;
            if (!Directory.Exists(dir)) // broken dir on some test platforms 
                dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir, $"test-{name}-{Guid.NewGuid()}.tmp");
        }
    }
}
