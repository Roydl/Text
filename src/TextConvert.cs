namespace Roydl.Text
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Properties;

    /// <summary>
    ///     Provides static methods for converting text.
    /// </summary>
    public static class TextConvert
    {
        /// <summary>
        ///     Rotates all ASCII letters of the specified text by 13 characters.
        /// </summary>
        /// <param name="text">
        ///     The string to convert.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     text is null.
        /// </exception>
        /// <returns>
        ///     A string in which all ASCII letters are rotated by 13 characters.
        /// </returns>
        public static string Rot13(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return text.Any(TextVerify.IsAsciiLetter) ? new string(text.Select(Rot13).ToArray()) : text;
        }

        /// <summary>
        ///     Rotates the specified character by 13 characters if it is an ASCII letter,
        ///     otherwise the original character is returned.
        /// </summary>
        /// <param name="ch">
        ///     The character to convert.
        /// </param>
        /// <returns>
        ///     A converted ASCII letter or the original character.
        /// </returns>
        public static char Rot13(char ch)
        {
            switch (ch)
            {
                case >= 'A' and <= 'Z' when ch + 13 > 'Z':
                case >= 'a' and <= 'z' when ch + 13 > 'z':
                    return (char)(ch - 13);
                case >= 'A' and <= 'Z':
                case >= 'a' and <= 'z':
                    return (char)(ch + 13);
                default:
                    return ch;
            }
        }

        /// <summary>
        ///     Converts all line separator characters of the specified input stream into
        ///     the specified output stream.
        /// </summary>
        /// <param name="inputStream">
        ///     The input stream to convert.
        /// </param>
        /// <param name="outputStream">
        ///     The output stream for converting.
        /// </param>
        /// <param name="separator">
        ///     The new format to be applied.
        /// </param>
        /// <param name="maxInRow">
        ///     The maximum number of separators in a row. If the value is 0, all
        ///     separators are removed, if it is negative, this function is ignored.
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
        ///     An I/O error occurs, such as the stream is closed.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///     Methods were called after the inputStream or outputStream was closed.
        /// </exception>
        public static void FormatSeparators(StreamReader inputStream, StreamWriter outputStream, TextNewLine separator = TextNewLine.WindowsDefault, int maxInRow = -1, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                var newLine = TextSeparator.GetSeparator(separator);
                var isCrLf = IsCrLf(inputStream.BaseStream);
                var inRow = 0;
                var ba = new char[16384];
                int len;
                while ((len = inputStream.Read(ba, 0, ba.Length)) > 0)
                {
                    for (var i = 0; i < len; i++)
                    {
                        var c = ba[i];
                        if (isCrLf && c == TextSeparator.CarriageReturnChar)
                            continue;
                        if (TextVerify.IsLineSeparator(c))
                        {
                            if (maxInRow < 0 || inRow++ < maxInRow)
                                outputStream.Write(newLine);
                            continue;
                        }
                        if (inRow > 0)
                            inRow = 0;
                        outputStream.Write(ba[i]);
                    }
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

        /// <inheritdoc cref="FormatSeparators(StreamReader, StreamWriter, TextNewLine, int, bool)"/>
        public static void FormatSeparators(Stream inputStream, Stream outputStream, TextNewLine separator = TextNewLine.WindowsDefault, int maxInRow = -1, bool dispose = false)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));
            try
            {
                using var sr = new StreamReader(inputStream, null, true, -1, true);
                using var sw = new StreamWriter(outputStream, sr.CurrentEncoding, -1, true);
                FormatSeparators(sr, sw, separator, maxInRow);
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
        ///     Converts all line separator characters of the specified source file to the
        ///     specified destination file.
        /// </summary>
        /// <param name="srcPath">
        ///     The source file to convert.
        /// </param>
        /// <param name="destPath">
        ///     The destination file to create.
        /// </param>
        /// <param name="separator">
        ///     The new format to be applied.
        /// </param>
        /// <param name="maxInRow">
        ///     The maximum number of separators in a row. If the value is 0, all
        ///     separators are removed, if it is negative, this function is ignored.
        /// </param>
        /// <param name="overwrite">
        ///     <see langword="true"/> to allow an existing file to be overwritten;
        ///     otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>
        ///     <see langword="true"/> if the destination file exists; otherwise,
        ///     <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     srcPath or destPath is null.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        ///     srcPath cannot be found.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        ///     destPath is invalid.
        /// </exception>
        /// <exception cref="IOException">
        ///     An I/O error occurred, such as the specified file cannot be found.
        /// </exception>
        public static bool FormatSeparators(string srcPath, string destPath, TextNewLine separator = TextNewLine.WindowsDefault, int maxInRow = -1, bool overwrite = true)
        {
            if (srcPath == null)
                throw new ArgumentNullException(nameof(srcPath));
            if (destPath == null)
                throw new ArgumentNullException(nameof(destPath));
            if (!File.Exists(srcPath))
                throw new FileNotFoundException(ExceptionMessages.FileNotFound, srcPath);
            var dir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException(ExceptionMessages.DestPathNotValid);
            using var fsi = new FileStream(srcPath, FileMode.Open, FileAccess.Read);
            using var fso = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew);
            FormatSeparators(fsi, fso, separator, maxInRow);
            return File.Exists(destPath);
        }

        /// <summary>
        ///     Converts all line separator characters of the specified string.
        /// </summary>
        /// <param name="text">
        ///     The text to change.
        /// </param>
        /// <param name="separator">
        ///     The new format to be applied.
        /// </param>
        /// <param name="maxInRow">
        ///     The maximum number of separators in a row. If the value is 0, all
        ///     separators are removed, if it is negative, this function is ignored.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     text is null.
        /// </exception>
        /// <inheritdoc cref="FormatSeparators(StreamReader, StreamWriter, TextNewLine, int, bool)"/>
        public static string FormatSeparators(string text, TextNewLine separator = TextNewLine.WindowsDefault, int maxInRow = -1)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (text == string.Empty)
                return text;
            using var msi = new MemoryStream(Encoding.UTF8.GetBytes(text));
            using var mso = new MemoryStream();
            FormatSeparators(msi, mso, separator, maxInRow);
            return Encoding.UTF8.GetString(mso.ToArray());
        }

        private static bool IsCrLf(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            var pos = stream.Position;
            var crLf = 0;
            int i;
            while ((i = stream.ReadByte()) != -1)
            {
                if (crLf > 0 && i == TextSeparator.LineFeedChar)
                {
                    crLf++;
                    break;
                }
                crLf = i == TextSeparator.CarriageReturnChar ? 1 : 0;
            }
            stream.Position = pos;
            return crLf > 1;
        }
    }
}
