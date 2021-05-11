namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Properties;

    /// <summary>
    ///     Provides functionality for encoding data into the Base-32 text
    ///     representations and back.
    /// </summary>
    public sealed class Base32 : BinaryToTextEncoding
    {
        private static readonly byte[] DefCharacterTable32 =
        {
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
            0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50,
            0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
            0x59, 0x5a, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37
        };

        /// ReSharper disable CommentTypo
        /// <summary>
        ///     Standard 32-character set: <code>ABCDEFGHIJKLMNOPQRSTUVWXYZ234567</code>
        /// </summary>
        /// ReSharper restore CommentTypo
        private static ReadOnlySpan<byte> CharacterTable32 => DefCharacterTable32;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Base32"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base32() { }

        /// <summary>
        ///     Encodes the specified input stream into the specified output stream.
        /// </summary>
        /// <param name="inputStream">
        ///     The input stream to encode.
        /// </param>
        /// <param name="outputStream">
        ///     The output stream for encoding.
        /// </param>
        /// <param name="lineLength">
        ///     The length of lines.
        /// </param>
        /// <param name="dispose">
        ///     <see langword="true"/> to release all resources used by the input and
        ///     output <see cref="Stream"/>; otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     inputStream or outputStream is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     inputStream or outputStream is invalid.
        /// </exception>
        /// <exception cref="NotSupportedException">
        ///     inputStream is not readable -or- outputStream is not writable.
        /// </exception>
        /// <exception cref="IOException">
        ///     An I/O error occurred, such as the specified file cannot be found.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Methods were called after the inputStream or outputStream was closed.
        /// </exception>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                var pos = 0;
                var size = 0;
                var r = 5;
                var c = 0;
                int b;
                while ((b = inputStream.ReadByte()) != -1)
                {
                    c = (c | (b >> (8 - r))) & 0xff;
                    size++;
                    WriteLine(outputStream, CharacterTable32[c], lineLength, ref pos);
                    if (r < 4)
                    {
                        c = (b >> (3 - r)) & 0x1f;
                        size++;
                        WriteLine(outputStream, CharacterTable32[c], lineLength, ref pos);
                        r += 5;
                    }
                    r -= 3;
                    c = (b << r) & 0x1f;
                }
                if (size == (int)Math.Ceiling(inputStream.Length / 5d) * 8)
                    return;
                WriteLine(outputStream, CharacterTable32[c], lineLength, ref pos);
                while (++size % 8 != 0)
                    WriteLine(outputStream, (byte)'=', lineLength, ref pos);
            }
            finally
            {
                if (dispose)
                {
                    inputStream.Dispose();
                    outputStream.Dispose();
                }
            }
        }

        /// <summary>
        ///     Decodes the specified input stream into the specified output stream.
        /// </summary>
        /// <param name="inputStream">
        ///     The input stream to decode.
        /// </param>
        /// <param name="outputStream">
        ///     The output stream for decoding.
        /// </param>
        /// <param name="dispose">
        ///     <see langword="true"/> to release all resources used by the input and
        ///     output <see cref="Stream"/>; otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     inputStream or outputStream is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     inputStream or outputStream is invalid.
        /// </exception>
        /// <exception cref="DecoderFallbackException">
        ///     inputStream contains invalid characters.
        /// </exception>
        /// <exception cref="NotSupportedException">
        ///     inputStream is not readable -or- outputStream is not writable.
        /// </exception>
        /// <exception cref="IOException">
        ///     An I/O error occurred, such as the specified file cannot be found.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Methods were called after the inputStream or outputStream was closed.
        /// </exception>
        public override void DecodeStream(Stream inputStream, Stream outputStream, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                var buf = new byte[16384];
                int len;
                while ((len = inputStream.Read(buf, 0, buf.Length)) > 0)
                {
                    var cleaned = buf.Take(len).Where(b => !IsSkippable(b)).TakeWhile(b => b != '=').ToArray();
                    var size = cleaned.Length * 5;
                    for (var j = 0; j < size; j += 8)
                    {
                        var b = cleaned[j / 5];
                        var c = CharacterTable32.IndexOf(b) << 10;
                        var n = j / 5 + 1;
                        if (n < cleaned.Length)
                        {
                            b = cleaned[n];
                            LocalDecoderFallbackCheck(b);
                            var p = CharacterTable32.IndexOf(b);
                            c |= p << 5;
                            c |= p << 5;
                        }
                        if (++n < cleaned.Length)
                        {
                            b = cleaned[n];
                            LocalDecoderFallbackCheck(b);
                            c |= CharacterTable32.IndexOf(b);
                        }
                        c = 255 & (c >> (15 - j % 5 - 8));
                        if (j + 5 > size && c < 1)
                            break;
                        outputStream.WriteByte((byte)c);
                    }
                }

                static void LocalDecoderFallbackCheck(int i)
                {
                    if (i is (< '2' or > '7') and (< 'A' or > 'Z'))
                        throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)i));
                }
            }
            finally
            {
                if (dispose)
                {
                    inputStream.Dispose();
                    outputStream.Dispose();
                }
            }
        }
    }
}
