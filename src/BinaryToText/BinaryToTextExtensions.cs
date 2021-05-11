namespace Roydl.Text.BinaryToText
{
    using System;

    /// <summary>
    ///     Specifies enumerated constants used to encode and decode data.
    /// </summary>
    public enum BinToTextEncoding
    {
        /// <summary>
        ///     Binary numeral system (also called base-2).
        /// </summary>
        Base02,

        /// <summary>
        ///     Octal numeral system (also called base-8).
        /// </summary>
        Base08,

        /// <summary>
        ///     Decimal numeral system (also called base-10).
        /// </summary>
        Base10,

        /// <summary>
        ///     Hexadecimal numeral system (also called base-16).
        /// </summary>
        Base16,

        /// <summary>
        ///     Base-32 scheme.
        /// </summary>
        Base32,

        /// <summary>
        ///     Base-64 scheme.
        /// </summary>
        Base64,

        /// <summary>
        ///     Base-85 scheme (also called Ascii85).
        /// </summary>
        Base85,

        /// <summary>
        ///     Base-91 scheme (formerly written basE91).
        /// </summary>
        Base91
    }

    /// <summary>
    ///     Provides extension methods for data encryption and decryption.
    /// </summary>
    public static class BinaryToTextExtensions
    {
        private static BinaryToTextEncoding[] _defaultInstances;

        private static ReadOnlySpan<BinaryToTextEncoding> DefaultInstances
        {
            get
            {
                if (_defaultInstances != null)
                    return _defaultInstances;
                _defaultInstances = new BinaryToTextEncoding[]
                {
                    new Base02(),
                    new Base08(),
                    new Base10(),
                    new Base16(),
                    new Base32(),
                    new Base64(),
                    new Base85(),
                    new Base91()
                };
                return _defaultInstances;
            }
        }

        /// <summary>
        ///     Retrieves a static default instance of the specified encoder.
        /// </summary>
        /// <param name="encoder">
        /// </param>
        /// <returns>
        ///     A static default instance of the specified encoder.
        /// </returns>
        public static BinaryToTextEncoding GetDefaultInstance(this BinToTextEncoding encoder)
        {
            var i = (int)encoder;
            if (i > DefaultInstances.Length)
                throw new ArgumentOutOfRangeException(nameof(encoder));
            return DefaultInstances[i];
        }

        /// <summary>
        ///     Encodes this sequence of bytes with the specified encoder.
        /// </summary>
        /// <param name="bytes">
        ///     The sequence of bytes to encode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A string that contains the result of encoding the specified sequence of
        ///     bytes by the specified encoder.
        /// </returns>
        public static string Encode(this byte[] bytes, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().EncodeBytes(bytes);

        /// <summary>
        ///     Encodes this string with the specified encoder.
        /// </summary>
        /// <param name="text">
        ///     The string to encode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A string that contains the result of encoding the specified text by the
        ///     specified encoder.
        /// </returns>
        public static string Encode(this string text, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().EncodeString(text);

        /// <summary>
        ///     Encodes this file with the specified encoder.
        /// </summary>
        /// <param name="path">
        ///     The full path of the file to encode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A string that contains the result of encoding the specified file by the
        ///     specified encoder.
        /// </returns>
        public static string EncodeFile(this string path, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().EncodeFile(path);

        /// <summary>
        ///     Decodes this string into a sequence of bytes with the specified encoder.
        /// </summary>
        /// <param name="code">
        ///     The string to decode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A sequence of bytes that contains the results of decoding the specified
        ///     code by the specified encoder.
        /// </returns>
        public static byte[] Decode(this string code, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().DecodeBytes(code);

        /// <summary>
        ///     Decodes this string into a sequence of bytes with the specified encoder.
        /// </summary>
        /// <param name="code">
        ///     The string to decode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A string that contains the result of decoding the specified code by the
        ///     specified encoder.
        /// </returns>
        public static string DecodeString(this string code, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().DecodeString(code);

        /// <summary>
        ///     Decodes this file into a sequence of bytes with the specified encoder.
        /// </summary>
        /// <param name="path">
        ///     The full path of the file to decode.
        /// </param>
        /// <param name="encoder">
        ///     The encoder to use.
        /// </param>
        /// <returns>
        ///     A sequence of bytes that contains the results of decoding the specified
        ///     file by the specified encoder.
        /// </returns>
        public static byte[] DecodeFile(this string path, BinToTextEncoding encoder = BinToTextEncoding.Base64) =>
            encoder.GetDefaultInstance().DecodeFile(path);
    }
}
