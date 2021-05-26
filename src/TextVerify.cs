namespace Roydl.Text
{
    /// <summary>Provides static methods of doing some checks on a character.</summary>
    public static class TextVerify
    {
        /// <summary>Indicates whether the specified character is categorized as an ASCII.</summary>
        /// <param name="ch">The character to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is ASCII; otherwise, <see langword="false"/>.</returns>
        public static bool IsAscii(char ch) =>
            ch <= sbyte.MaxValue;

        /// <summary>Indicates whether the specified character is categorized as Latin-1 (ISO-8859-1).</summary>
        /// <param name="ch">The character to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is Latin-1 (ISO-8859-1); otherwise, <see langword="false"/>.</returns>
        public static bool IsLatin1(char ch) =>
            ch <= byte.MaxValue;

        /// <summary>Indicates whether the specified character is categorized as an ASCII letter.</summary>
        /// <param name="ch">The character to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is a ASCII letter; otherwise, <see langword="false"/>.</returns>
        public static bool IsAsciiLetter(char ch) =>
            IsAscii(ch) && char.IsLetter(ch);

        /// <summary>Indicates whether the specified character is categorized as an Latin-1 (ISO-8859-1) letter.</summary>
        /// <param name="ch">The character to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is a Latin-1 (ISO-8859-1) letter; otherwise, <see langword="false"/>.</returns>
        public static bool IsLatin1Letter(char ch) =>
            IsLatin1(ch) && char.IsLetter(ch);

        /// <summary>Indicates whether the specified character is categorized as line separator.</summary>
        /// <param name="ch">The character to evaluate.</param>
        /// <remarks>See <seealso cref="TextNewLine"/> for more information.</remarks>
        /// <returns><see langword="true"/> if <paramref name="ch"/> is a line separator; otherwise, <see langword="false"/>.</returns>
        public static bool IsLineSeparator(char ch)
        {
            switch (ch)
            {
                case TextSeparator.LineFeedChar:
                case TextSeparator.VerticalTabChar:
                case TextSeparator.FormFeedChar:
                case TextSeparator.CarriageReturnChar:
                case TextSeparator.NextLineChar:
                case TextSeparator.BoundaryNeutralChar:
                case TextSeparator.LineSeparatorChar:
                case TextSeparator.ParagraphSeparatorChar:
                    return true;
            }
            return false;
        }
    }
}
