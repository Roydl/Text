namespace Roydl.Text.Internal
{
    using System;
    using System.IO;

    internal static class Helper
    {
        internal static BufferedStream GetBufferedStream(Stream stream, int size = 0) =>
            stream switch
            {
                null => throw new ArgumentNullException(nameof(stream)),
                BufferedStream bs => bs,
                _ => new BufferedStream(stream, size > 0 ? size : GetBufferSize(stream))
            };

        internal static int GetBufferSize(Stream stream)
        {
            const int kb128 = 0x20000;
            const int kb64 = 0x10000;
            const int kb32 = 0x8000;
            const int kb16 = 0x4000;
            const int kb8 = 0x2000;
            const int kb4 = 0x1000;
            return (int)Math.Floor((stream?.Length ?? 0) / 1.5d) switch
            {
                > kb128 => kb128,
                > kb64 => kb64,
                > kb32 => kb32,
                > kb16 => kb16,
                > kb8 => kb8,
                _ => kb4
            };
        }

        internal static int GetBufferSize(StreamReader stream) =>
            GetBufferSize(stream?.BaseStream);
    }
}
