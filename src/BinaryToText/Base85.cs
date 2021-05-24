namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Internal;
    using Resources;

    /// <summary>
    ///     Provides functionality for encoding data into the Base-85 (also called
    ///     Ascii85) text representation and back.
    /// </summary>
    public sealed class Base85 : BinaryToTextEncoding
    {
        private static readonly uint[] DefPow85 =
        {
            85 * 85 * 85 * 85,
            85 * 85 * 85,
            85 * 85,
            85,
            1
        };

        private static ReadOnlySpan<uint> Pow85 => DefPow85;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Base85"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base85() { }

        /// <inheritdoc/>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var eb = new byte[5].AsSpan();
                var pos = 0;
                var t = 0u;
                var n = 0;
                int b;
                while ((b = bsi.ReadByte()) != -1)
                {
                    if (n + 1 < 4)
                    {
                        t |= (uint)(b << (24 - n * 8));
                        n++;
                        continue;
                    }
                    t |= (uint)b;
                    if (t == 0)
                        WriteLine(bso, 0x7a, lineLength, ref pos);
                    else
                    {
                        for (var i = eb.Length - 1; i >= 0; i--)
                        {
                            eb[i] = (byte)(t % 85 + 33);
                            t /= 85;
                        }
                        WriteLine(bso, eb, lineLength, ref pos);
                    }
                    t = 0;
                    n = 0;
                }
                if (n <= 0)
                    return;
                for (var i = eb.Length - 1; i >= 0; i--)
                {
                    eb[i] = (byte)(t % 85 + 33);
                    t /= 85;
                }
                for (var i = 0; i <= n; i++)
                    WriteLine(bso, eb[i], lineLength, ref pos);
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
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var db = new byte[4].AsSpan();
                var t = 0u;
                var n = 0;
                int b;
                while ((b = bsi.ReadByte()) != -1)
                {
                    if (b == 'z')
                    {
                        if (n != 0)
                            throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, 'z'));
                        for (var i = 0; i < db.Length; i++)
                            db[i] = 0;
                        bso.Write(db);
                        continue;
                    }
                    if (IsSkippable(b))
                        continue;
                    if (b is < '!' or > 'u')
                        throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)b));
                    t += (uint)((b - 33) * Pow85[n]);
                    if (++n != 5)
                        continue;
                    for (var i = 0; i < db.Length; i++)
                        db[i] = (byte)(t >> (24 - i * 8));
                    bso.Write(db);
                    t = 0;
                    n = 0;
                }
                switch (n)
                {
                    case 0: return;
                    case 1: throw new DecoderFallbackException(ExceptionMessages.LastBlockIsSingleByte);
                }
                n--;
                t += Pow85[n];
                for (var i = 0; i < n; i++)
                    db[i] = (byte)(t >> (24 - i * 8));
                for (var i = 0; i < n; i++)
                    bso.WriteByte(db[i]);
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
