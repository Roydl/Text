﻿namespace Roydl.Text.BinaryToText
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;
    using Properties;

    /// <summary>
    ///     Provides functionality for encoding data into Base-8 (octal) text
    ///     representations and back.
    /// </summary>
    public sealed class Base08 : BinaryToTextEncoding
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Base08"/> class.
        /// </summary>
        [SuppressMessage("ReSharper", "EmptyConstructor")]
        public Base08() { }

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
                var p = 0;
                int i;
                while ((i = inputStream.ReadByte()) != -1)
                {
                    var s = Convert.ToString(i, 8).PadLeft(3, '0');
                    foreach (var b in Encoding.UTF8.GetBytes(s))
                        WriteLine(outputStream, b, lineLength, ref p);
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
                var cl = new List<char>();
                int i;
                while ((i = inputStream.ReadByte()) != -1)
                {
                    if (IsSkippable(i, ','))
                        continue;
                    if (i is not (>= '0' and <= '7'))
                        throw new DecoderFallbackException(ExceptionMessages.CharsInStreamAreInvalid);
                    cl.Add((char)i);
                    if (cl.Count % 3 != 0)
                        continue;
                    outputStream.WriteByte(Convert.ToByte(new string(cl.ToArray()), 8));
                    cl.Clear();
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
