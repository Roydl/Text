namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Internal;
    using Resources;

    /// <summary>Provides functionality for encoding data into the Base-91 (formerly written basE91) text representation and back.</summary>
    /// <remarks><b>Performance:</b> Poor by design. Base91 is inherently sequential — each output word depends on an accumulated bit-state carried over from the previous byte, forming a serial dependency chain. This makes parallel encoding and decoding impossible without a full pre-scan pass that replicates the entire computation, which negates any potential gain from parallelism.</remarks>
    /// <seealso href="http://base91.sourceforge.net/"/>
    public sealed class Base91 : BinaryToTextEncoding
    {
        /// <summary>Standard 91-character set:
        ///     <para><code>ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&amp;()*+,-.:;&lt;=&gt;?@[]^_`{|}~&quot;</code></para>
        /// </summary>
        private static readonly byte[] DefCharacterTable91 =
        [
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
            0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x51, 0x52,
            0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x61,
            0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a,
            0x6b, 0x6c, 0x6d, 0x6e, 0x6f, 0x70, 0x71, 0x72, 0x73,
            0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x30, 0x31,
            0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x21,
            0x23, 0x24, 0x25, 0x26, 0x28, 0x29, 0x2a, 0x2b, 0x2c,
            0x2d, 0x2e, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x40,
            0x5b, 0x5d, 0x5e, 0x5f, 0x60, 0x7b, 0x7c, 0x7d, 0x7e,
            0x22
        ];

        /// <inheritdoc cref="DefCharacterTable91"/>
        private static ReadOnlySpan<byte> CharacterTable91 => DefCharacterTable91;

        /// <summary>Initializes a new instance of the <see cref="Base91"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base91() { }

        /// <inheritdoc/>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputStream);
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var eb = new int[3].AsSpan();
                var pos = 0;
                int i;
                while ((i = bsi.ReadByte()) != -1)
                {
                    eb[0] |= i << eb[1];
                    eb[1] += 8;
                    if (eb[1] < 14)
                        continue;
                    eb[2] = eb[0] & 8191;
                    if (eb[2] > 88)
                    {
                        eb[1] -= 13;
                        eb[0] >>= 13;
                    }
                    else
                    {
                        eb[2] = eb[0] & 16383;
                        eb[1] -= 14;
                        eb[0] >>= 14;
                    }
                    WriteLine(bso, CharacterTable91[eb[2] % 91], lineLength, ref pos);
                    WriteLine(bso, CharacterTable91[eb[2] / 91], lineLength, ref pos);
                }
                if (eb[1] == 0)
                    return;
                WriteLine(bso, CharacterTable91[eb[0] % 91], lineLength, ref pos);
                if (eb[1] >= 8 || eb[0] >= 91)
                    WriteLine(bso, CharacterTable91[eb[0] / 91], lineLength, ref pos);
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
                var db = new[] { 0, -1, 0, 0 }.AsSpan();
                int i;
                while ((i = bsi.ReadByte()) != -1)
                {
                    if (IsSkippable(i))
                        continue;
                    if (!CharacterTable91.Contains((byte)i))
                        throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)i));
                    db[0] = CharacterTable91.IndexOf((byte)i);
                    if (db[0] == -1)
                        continue;
                    if (db[1] < 0)
                    {
                        db[1] = db[0];
                        continue;
                    }
                    db[1] += db[0] * 91;
                    db[2] |= db[1] << db[3];
                    db[3] += (db[1] & 8191) > 88 ? 13 : 14;
                    do
                    {
                        bso.WriteByte((byte)(db[2] & byte.MaxValue));
                        db[2] >>= 8;
                        db[3] -= 8;
                    }
                    while (db[3] > 7);
                    db[1] = -1;
                }
                if (db[1] != -1)
                    bso.WriteByte((byte)((db[2] | db[1] << db[3]) & byte.MaxValue));
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
    }
}
