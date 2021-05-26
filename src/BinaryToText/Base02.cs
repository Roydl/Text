namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Internal;
    using Resources;

    /// <summary>Provides functionality for encoding data into Base-2 (binary) text representations and back.</summary>
    public sealed class Base02 : BinaryToTextEncoding
    {
        /// <summary>Initializes a new instance of the <see cref="Base02"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base02() { }

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
                var pos = 0;
                int i;
                while ((i = bsi.ReadByte()) != -1)
                {
                    var s = Convert.ToString(i, 2).PadLeft(8, '0');
                    foreach (var b in Encoding.UTF8.GetBytes(s))
                        WriteLine(bso, b, lineLength, ref pos);
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
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            var bsi = Helper.GetBufferedStream(inputStream);
            var bso = Helper.GetBufferedStream(outputStream, bsi.BufferSize);
            try
            {
                var db = new List<char>();
                int i;
                while ((i = bsi.ReadByte()) != -1)
                {
                    if (IsSkippable(i, '-', ','))
                        continue;
                    if (i is not '0' and not '1')
                        throw new DecoderFallbackException(ExceptionMessages.CharsInStreamAreInvalid);
                    db.Add((char)i);
                    if (db.Count % 8 != 0)
                        continue;
                    bso.WriteByte(Convert.ToByte(new string(db.ToArray()), 2));
                    db.Clear();
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
    }
}
