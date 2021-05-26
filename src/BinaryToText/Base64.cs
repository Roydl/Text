namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using Internal;

    /// <summary>Provides functionality for encoding data into the Base-64 text representations and back.</summary>
    public sealed class Base64 : BinaryToTextEncoding
    {
        /// <summary>Initializes a new instance of the <see cref="Base64"/> class.</summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base64() { }

        /// <inheritdoc/>
        public override void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                using var cs = new CryptoStream(inputStream, new ToBase64Transform(), CryptoStreamMode.Read, true);
                var pos = 0;
                var ba = new byte[Helper.GetBufferSize(inputStream)];
                int len;
                while ((len = cs.Read(ba)) > 0)
                    WriteLine(outputStream, ba, len, lineLength, ref pos);
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
                using var fbt = new FromBase64Transform();
                var size = Helper.GetBufferSize(inputStream);
                var bai = new byte[size];
                var bao = new byte[size];
                int len;
                while ((len = inputStream.Read(bai)) > 0)
                {
                    var cleaned = bai.Take(len).Where(b => !IsSkippable(b)).ToArray();
                    len = fbt.TransformBlock(cleaned, 0, cleaned.Length, bao, 0);
                    outputStream.Write(bao, 0, len);
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
