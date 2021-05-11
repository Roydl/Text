namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Properties;

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

        private readonly byte[] _encodeBlock = new byte[5],
                                _decodeBlock = new byte[4];

        private static ReadOnlySpan<uint> Pow85 => DefPow85;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Base85"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base85() { }

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
                var t = 0u;
                var n = 0;
                int b;
                while ((b = inputStream.ReadByte()) != -1)
                {
                    if (n + 1 < _decodeBlock.Length)
                    {
                        t |= (uint)(b << (24 - n * 8));
                        n++;
                        continue;
                    }
                    t |= (uint)b;
                    if (t == 0)
                        WriteLine(outputStream, 0x7a, lineLength, ref pos);
                    else
                    {
                        for (var i = _encodeBlock.Length - 1; i >= 0; i--)
                        {
                            _encodeBlock[i] = (byte)(t % 85 + 33);
                            t /= 85;
                        }
                        foreach (var eb in _encodeBlock)
                            WriteLine(outputStream, eb, lineLength, ref pos);
                    }
                    t = 0;
                    n = 0;
                }
                if (n <= 0)
                    return;
                for (var i = _encodeBlock.Length - 1; i >= 0; i--)
                {
                    _encodeBlock[i] = (byte)(t % 85 + 33);
                    t /= 85;
                }
                for (var i = 0; i <= n; i++)
                    WriteLine(outputStream, _encodeBlock[i], lineLength, ref pos);
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
                var t = 0u;
                var n = 0;
                int b;
                while ((b = inputStream.ReadByte()) != -1)
                {
                    if (b == 'z')
                    {
                        if (n != 0)
                            throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, 'z'));
                        for (var i = 0; i < 4; i++)
                            _decodeBlock[i] = 0;
                        outputStream.Write(_decodeBlock, 0, _decodeBlock.Length);
                        continue;
                    }
                    if (IsSkippable(b))
                        continue;
                    if (b is < '!' or > 'u')
                        throw new DecoderFallbackException(string.Format(ExceptionMessages.CharIsInvalid, (char)b));
                    t += (uint)((b - 33) * Pow85[n]);
                    if (++n != _encodeBlock.Length)
                        continue;
                    for (var i = 0; i < _decodeBlock.Length; i++)
                        _decodeBlock[i] = (byte)(t >> (24 - i * 8));
                    outputStream.Write(_decodeBlock, 0, _decodeBlock.Length);
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
                    _decodeBlock[i] = (byte)(t >> (24 - i * 8));
                for (var i = 0; i < n; i++)
                    outputStream.WriteByte(_decodeBlock[i]);
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
