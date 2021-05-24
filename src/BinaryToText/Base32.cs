namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Internal;
    using Resources;

    /// <summary>
    ///     Provides functionality for encoding data into the Base-32 text
    ///     representations and back.
    /// </summary>
    public class Base32 : BinaryToTextEncoding
    {
        /// ReSharper disable CommentTypo
        /// <summary>
        ///     Standard 32-character set:
        ///     <para>
        ///         <code>ABCDEFGHIJKLMNOPQRSTUVWXYZ234567</code>
        ///     </para>
        /// </summary>
        /// ReSharper restore CommentTypo
        protected static readonly byte[] DefCharacterTable32 =
        {
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
            0x49, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50,
            0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
            0x59, 0x5a, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37
        };

        /// <inheritdoc cref="DefCharacterTable32"/>
        protected virtual ReadOnlySpan<byte> CharacterTable32 => DefCharacterTable32;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Base32"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base32() { }

        /// <inheritdoc/>
        public sealed override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var pos = 0;
                var size = 0;
                var r = 5;
                var c = 0;
                int b;
                while ((b = bsi.ReadByte()) != -1)
                {
                    c = (c | (b >> (8 - r))) & 0xff;
                    size++;
                    WriteLine(bso, CharacterTable32[c], lineLength, ref pos);
                    if (r < 4)
                    {
                        c = (b >> (3 - r)) & 0x1f;
                        size++;
                        WriteLine(bso, CharacterTable32[c], lineLength, ref pos);
                        r += 5;
                    }
                    r -= 3;
                    c = (b << r) & 0x1f;
                }
                if (size == (int)Math.Ceiling(bsi.Length / 5d) * 8)
                    return;
                WriteLine(bso, CharacterTable32[c], lineLength, ref pos);
                while (++size % 8 != 0)
                    WriteLine(bso, (byte)'=', lineLength, ref pos);
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
        public sealed override void DecodeStream(Stream inputStream, Stream outputStream, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            var size = Helper.GetBufferSize(inputStream);
            var bs = Helper.GetBufferedStream(outputStream, size);
            try
            {
                var ba = new byte[size];
                int len;
                while ((len = inputStream.Read(ba, 0, ba.Length)) > 0)
                {
                    var cleaned = ba.Take(len).Where(b => !IsSkippable(b)).TakeWhile(b => b != '=').ToArray();
                    var cleanSize = cleaned.Length * 5;
                    for (var j = 0; j < cleanSize; j += 8)
                    {
                        var b = cleaned[j / 5];
                        LocalDecoderFallbackCheck(b);
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
                        c = 0xff & (c >> (15 - j % 5 - 8));
                        if (j + 5 > cleanSize && c < 1)
                            break;
                        bs.WriteByte((byte)c);
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
                    bs.Dispose();
                }
                else
                    bs.Flush();
            }
        }
    }
}
