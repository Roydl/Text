namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Properties;

    /// <summary>
    ///     Provides functionality for encoding data into Base-10 (decimal) text
    ///     representations and back.
    /// </summary>
    public sealed class Base10 : BinaryToTextEncoding
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Base10"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base10() { }

        /// <inheritdoc/>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                var pos = 0;
                int i;
                while ((i = inputStream.ReadByte()) != -1)
                {
                    var s = Convert.ToString(i, 10).PadLeft(3, '0');
                    foreach (var b in Encoding.UTF8.GetBytes(s))
                        WriteLine(outputStream, b, lineLength, ref pos);
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

        /// <inheritdoc/>
        public override void DecodeStream(Stream inputStream, Stream outputStream, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                var db = new List<char>();
                int i;
                while ((i = inputStream.ReadByte()) != -1)
                {
                    if (IsSkippable(i, ','))
                        continue;
                    if (i is not (>= '0' and <= '9'))
                        throw new DecoderFallbackException(ExceptionMessages.CharsInStreamAreInvalid);
                    db.Add((char)i);
                    if (db.Count % 3 != 0)
                        continue;
                    outputStream.WriteByte(Convert.ToByte(new string(db.ToArray()), 10));
                    db.Clear();
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
