namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Threading;

    /// <summary>Specifies enumerated constants used to encode and decode data.</summary>
    public enum BinToTextEncoding
    {
        /// <summary>Binary numeral system (also called base-2).</summary>
        Base02,

        /// <summary>Octal numeral system (also called base-8).</summary>
        Base08,

        /// <summary>Decimal numeral system (also called base-10).</summary>
        Base10,

        /// <summary>Hexadecimal numeral system (also called base-16).</summary>
        Base16,

        /// <summary>Base-32 scheme.</summary>
        Base32,

        /// <summary>Base-64 scheme.</summary>
        Base64,

        /// <summary>Base-85 scheme (also called Ascii85).</summary>
        Base85,

        /// <summary>Base-91 scheme (formerly written basE91).</summary>
        Base91
    }

    /// <summary>Provides extension methods for data encryption and decryption.</summary>
    public static class BinaryToTextExtensions
    {
        private static volatile BinaryToTextEncoding[] _cachedInstances;

        /// <summary>Encodes this sequence of bytes with the specified encoder.</summary>
        /// <param name="bytes">The sequence of bytes to encode.</param>
        /// <param name="encoder">The encoder to use.</param>
        /// <inheritdoc cref="BinaryToTextEncoding.EncodeBytes(byte[], int)"/>
        public static string Encode(this byte[] bytes, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().EncodeBytes(bytes);

        /// <summary>Retrieves a cached instance of the specified encoder.</summary>
        /// <param name="encoder"></param>
        /// <returns>A cached instance of the specified encoder.</returns>
        public static BinaryToTextEncoding GetDefaultInstance(this BinToTextEncoding encoder)
        {
            var i = (int)encoder;
            while (_cachedInstances == null)
                Interlocked.CompareExchange(ref _cachedInstances, new BinaryToTextEncoding[Enum.GetValues(typeof(BinToTextEncoding)).Length], null);
            while (_cachedInstances[i] == null)
                Interlocked.CompareExchange(ref _cachedInstances[i], encoder switch
                {
                    BinToTextEncoding.Base02 => new Base02(),
                    BinToTextEncoding.Base08 => new Base08(),
                    BinToTextEncoding.Base10 => new Base10(),
                    BinToTextEncoding.Base16 => new Base16(),
                    BinToTextEncoding.Base32 => new Base32(),
                    BinToTextEncoding.Base64 => new Base64(),
                    BinToTextEncoding.Base85 => new Base85(),
                    BinToTextEncoding.Base91 => new Base91(),
                    _ => throw new ArgumentOutOfRangeException(nameof(encoder), encoder, null)
                }, null);
            return _cachedInstances[i];
        }

        /// <param name="text">The string to encode.</param>
        extension(string text)
        {
            /// <summary>Encodes this string with the specified encoder.</summary>
            /// <param name="encoder">The encoder to use.</param>
            /// <inheritdoc cref="BinaryToTextEncoding.EncodeString(string, int)"/>
            public string Encode(BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
                encoder.GetDefaultInstance().EncodeString(text);

            /// <summary>Encodes this file with the specified encoder.</summary>
            /// <param name="encoder">The encoder to use.</param>
            /// <inheritdoc cref="BinaryToTextEncoding.EncodeFile(string, int)"/>
            public string EncodeFile(BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
                encoder.GetDefaultInstance().EncodeFile(text);

            /// <summary>Decodes this string into a sequence of bytes with the specified encoder.</summary>
            /// <param name="encoder">The encoder to use.</param>
            /// <inheritdoc cref="BinaryToTextEncoding.DecodeBytes(string)"/>
            public byte[] Decode(BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
                encoder.GetDefaultInstance().DecodeBytes(text);

            /// <summary>Decodes this string into a sequence of bytes with the specified encoder.</summary>
            /// <param name="encoder">The encoder to use.</param>
            /// <inheritdoc cref="BinaryToTextEncoding.DecodeString(string)"/>
            public string DecodeString(BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
                encoder.GetDefaultInstance().DecodeString(text);

            /// <summary>Decodes this file into a sequence of bytes with the specified encoder.</summary>
            /// <param name="encoder">The encoder to use.</param>
            /// <inheritdoc cref="BinaryToTextEncoding.DecodeFile(string)"/>
            public byte[] DecodeFile(BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
                encoder.GetDefaultInstance().DecodeFile(text);
        }
    }
}
