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
            const int mb1 = 0x100000;
            const int kb512 = 0x80000;
            const int kb256 = 0x40000;
            const int kb128 = 0x20000;
            const int kb64 = 0x10000;
            const int kb16 = 0x4000;

            long length;
            try
            {
                length = stream?.Length ?? 0;
            }
            catch
            {
                length = 0;
            }

            return length switch
            {
                > mb1 => mb1,
                > kb512 => kb512,
                > kb256 => kb256,
                > kb128 => kb128,
                > kb64 => kb64,
                _ => kb16
            };
        }

        internal static int GetBufferSize(StreamReader stream) =>
            GetBufferSize(stream?.BaseStream);
    }
}
