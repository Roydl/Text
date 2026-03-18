namespace Roydl.Text.BinaryToText
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Resources;

    /// <summary>Represents the base class from which all implementations of binary-to-text encoding must derive.</summary>
    public abstract class BinaryToTextEncoding
    {
        /// <summary>Gets <see cref="Environment.NewLine"/> as a sequence of bytes used as a line separator when a line length is defined.</summary>
        protected virtual ReadOnlyMemory<byte> Separator { get; } = Encoding.UTF8.GetBytes(Environment.NewLine);

        /// <summary>Encodes the specified input stream into the specified output stream.</summary>
        /// <param name="inputStream">The input stream to encode.</param>
        /// <param name="outputStream">The output stream for encoding.</param>
        /// <param name="lineLength">The length of lines.</param>
        /// <param name="dispose"><see langword="true"/> to release all resources used by the input and output <see cref="Stream"/>; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ArgumentNullException">inputStream or outputStream is null.</exception>
        /// <exception cref="ArgumentException">inputStream or outputStream is invalid.</exception>
        /// <exception cref="NotSupportedException">inputStream is not readable -or- outputStream is not writable.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the inputStream or outputStream was closed.</exception>
        public abstract void EncodeStream(Stream inputStream, Stream outputStream, int lineLength = 0, bool dispose = false);

        /// <inheritdoc cref="EncodeStream(Stream, Stream, int, bool)"/>
        public void EncodeStream(Stream inputStream, Stream outputStream, bool dispose) =>
            EncodeStream(inputStream, outputStream, 0, dispose);

        /// <summary>Encodes the specified sequence of bytes.</summary>
        /// <param name="bytes">The sequence of bytes to encode.</param>
        /// <param name="lineLength">The length of lines.</param>
        /// <exception cref="ArgumentNullException">bytes is null.</exception>
        /// <returns>A string that contains the result of encoding the specified sequence of bytes.</returns>
        public string EncodeBytes(byte[] bytes, int lineLength = 0)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            EncodeStream(msi, mso, lineLength);
            return Encoding.UTF8.GetString(mso.ToArray());
        }

        /// <summary>Encodes the specified string.</summary>
        /// <param name="text">The string to encode.</param>
        /// <param name="lineLength">The length of lines.</param>
        /// <exception cref="ArgumentNullException">text is null.</exception>
        /// <returns>A string that contains the result of encoding the specified string.</returns>
        public string EncodeString(string text, int lineLength = 0)
        {
            ArgumentNullException.ThrowIfNull(text);
            var ba = Encoding.UTF8.GetBytes(text);
            return EncodeBytes(ba, lineLength);
        }

        /// <summary>Encodes the specified source file to the specified destination file.</summary>
        /// <param name="srcPath">The source file to encode.</param>
        /// <param name="destPath">The destination file to create.</param>
        /// <param name="lineLength">The length of lines.</param>
        /// <param name="overwrite"><see langword="true"/> to allow an existing file to be overwritten; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ArgumentNullException">srcPath or destPath is null.</exception>
        /// <exception cref="FileNotFoundException">srcPath cannot be found.</exception>
        /// <exception cref="DirectoryNotFoundException">destPath is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <returns><see langword="true"/> if the destination file exists; otherwise, <see langword="false"/>.</returns>
        public bool EncodeFile(string srcPath, string destPath, int lineLength = 0, bool overwrite = true)
        {
            ArgumentNullException.ThrowIfNull(srcPath);
            ArgumentNullException.ThrowIfNull(destPath);
            if (!File.Exists(srcPath))
                throw new FileNotFoundException(ExceptionMessages.FileNotFound, srcPath);
            var dir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException(ExceptionMessages.DestPathNotValid);
            using var fsi = new FileStream(srcPath, FileMode.Open, FileAccess.Read);
            using var fso = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew);
            EncodeStream(fsi, fso, lineLength);
            return File.Exists(destPath);
        }

        /// <summary>Encodes the specified file.</summary>
        /// <param name="path">The file to encode.</param>
        /// <param name="lineLength">The length of lines.</param>
        /// <exception cref="ArgumentNullException">path is null.</exception>
        /// <exception cref="FileNotFoundException">path cannot be found.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <returns>A string that contains the result of encoding the file in the specified path.</returns>
        public string EncodeFile(string path, int lineLength = 0)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (!File.Exists(path))
                throw new FileNotFoundException(ExceptionMessages.FileNotFound, path);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var ms = new MemoryStream();
            EncodeStream(fs, ms, lineLength);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>Decodes the specified input stream into the specified output stream.</summary>
        /// <param name="inputStream">The input stream to decode.</param>
        /// <param name="outputStream">The output stream for decoding.</param>
        /// <param name="dispose"><see langword="true"/> to release all resources used by the input and output <see cref="Stream"/>; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ArgumentNullException">inputStream or outputStream is null.</exception>
        /// <exception cref="ArgumentException">inputStream or outputStream is invalid.</exception>
        /// <exception cref="DecoderFallbackException">inputStream contains invalid characters.</exception>
        /// <exception cref="NotSupportedException">inputStream is not readable -or- outputStream is not writable.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the inputStream or outputStream was closed.</exception>
        public abstract void DecodeStream(Stream inputStream, Stream outputStream, bool dispose = false);

        /// <summary>Decodes the specified string into a sequence of bytes.</summary>
        /// <param name="code">The string to decode.</param>
        /// <exception cref="ArgumentNullException">code is null.</exception>
        /// <exception cref="DecoderFallbackException">code contains invalid characters.</exception>
        /// <returns>A sequence of bytes that contains the results of decoding the specified string.</returns>
        public byte[] DecodeBytes(string code)
        {
            ArgumentNullException.ThrowIfNull(code);
            var ba = Encoding.UTF8.GetBytes(code);
            using var msi = new MemoryStream(ba);
            using var mso = new MemoryStream();
            DecodeStream(msi, mso);
            return mso.ToArray();
        }

        /// <summary>Decodes the specified string into a string.</summary>
        /// <param name="code">The string to decode.</param>
        /// <exception cref="ArgumentNullException">code is null.</exception>
        /// <exception cref="DecoderFallbackException">code contains invalid characters.</exception>
        /// <returns>A string that contains the result of decoding the specified string.</returns>
        public string DecodeString(string code)
        {
            var ba = DecodeBytes(code);
            return ba == null ? throw new NullReferenceException() : Encoding.UTF8.GetString(ba);
        }

        /// <summary>Decodes the specified source file to the specified destination file.</summary>
        /// <param name="srcPath">The source file to encode.</param>
        /// <param name="destPath">The destination file to create.</param>
        /// <param name="overwrite"><see langword="true"/> to allow an existing file to be overwritten; otherwise, <see langword="false"/>.</param>
        /// <exception cref="ArgumentNullException">srcPath or destPath is null.</exception>
        /// <exception cref="FileNotFoundException">srcPath cannot be found.</exception>
        /// <exception cref="DirectoryNotFoundException">destPath is invalid.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <exception cref="DecoderFallbackException">srcPath file contains invalid characters.</exception>
        /// <returns><see langword="true"/> if the destination file exists; otherwise, <see langword="false"/>.</returns>
        public bool DecodeFile(string srcPath, string destPath, bool overwrite = true)
        {
            ArgumentNullException.ThrowIfNull(srcPath);
            ArgumentNullException.ThrowIfNull(destPath);
            if (!File.Exists(srcPath))
                throw new FileNotFoundException(ExceptionMessages.FileNotFound, srcPath);
            var dir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException(ExceptionMessages.DestPathNotValid);
            using var fsi = new FileStream(srcPath, FileMode.Open, FileAccess.Read);
            using var fso = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew);
            DecodeStream(fsi, fso);
            return File.Exists(destPath);
        }

        /// <summary>Decodes the specified string into a sequence of bytes containing a small file.</summary>
        /// <param name="path">The file to decode.</param>
        /// <exception cref="ArgumentNullException">path is null.</exception>
        /// <exception cref="FileNotFoundException">path cannot be found.</exception>
        /// <exception cref="IOException">An I/O error occurred, such as the specified file cannot be found.</exception>
        /// <exception cref="DecoderFallbackException">srcPath file contains invalid characters.</exception>
        /// <returns>A sequence of bytes that contains the results of decoding the file in specified string.</returns>
        public byte[] DecodeFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (!File.Exists(path))
                throw new FileNotFoundException(ExceptionMessages.FileNotFound, path);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var ms = new MemoryStream();
            DecodeStream(fs, ms);
            return ms.ToArray();
        }

        /// <summary>Determines whether the specified value can be ignored.</summary>
        /// <param name="value">The character to check.</param>
        /// <param name="additional">Additional characters to be skipped.</param>
        /// <returns><see langword="true"/> if the byte number matches one of the characters; otherwise, <see langword="false"/>.</returns>
        protected static bool IsSkippable(int value, params int[] additional) =>
            value is '\0' or '\t' or '\n' or '\r' or ' ' || additional?.Any(i => value == i) == true;

        /// <summary>Reads bytes from the specified stream into the buffer until the buffer is full or the stream is exhausted.</summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to read into.</param>
        /// <returns>The total number of bytes read. May be less than the buffer length if the stream was exhausted.</returns>
        /// <exception cref="ArgumentNullException">stream is null.</exception>
        /// <exception cref="NotSupportedException">stream is not readable.</exception>
        /// <exception cref="IOException">An I/O error occurred while reading from the stream.</exception>
        /// <exception cref="ObjectDisposedException">Methods were called after the stream was closed.</exception>
        protected static int ReadBuffer(Stream stream, byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var totalRead = 0;
            int read;
            while (totalRead < buffer.Length && (read = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
                totalRead += read;
            return totalRead;
        }
    }
}
