namespace Roydl.Text
{
    using System;

    /// <summary>
    ///     Specifies enumerated constants that are used to determine line separators.
    /// </summary>
    public enum TextNewLine
    {
        /// <inheritdoc cref="TextSeparator.BoundaryNeutral"/>
        BoundaryNeutral = TextWhiteSpace.BoundaryNeutral,

        /// <inheritdoc cref="TextSeparator.CarriageReturn"/>
        CarriageReturn = TextWhiteSpace.CarriageReturn,

        /// <inheritdoc cref="TextSeparator.FormFeed"/>
        FormFeed = TextWhiteSpace.FormFeed,

        /// <inheritdoc cref="TextSeparator.LineFeed"/>
        LineFeed = TextWhiteSpace.LineFeed,

        /// <inheritdoc cref="TextSeparator.LineSeparator"/>
        LineSeparator = TextWhiteSpace.LineSeparator,

        /// <inheritdoc cref="TextSeparator.NextLine"/>
        NextLine = TextWhiteSpace.NextLine,

        /// <inheritdoc cref="TextSeparator.ParagraphSeparator"/>
        ParagraphSeparator = TextWhiteSpace.ParagraphSeparator,

        /// <inheritdoc cref="TextSeparator.VerticalTab"/>
        VerticalTab = TextWhiteSpace.VerticalTab,

        /// <inheritdoc cref="TextSeparator.WindowsDefault"/>
        WindowsDefault = TextWhiteSpace.WindowsDefault
    }

    /// <summary>
    ///     Specifies enumerated constants that are used to determine whitespaces.
    /// </summary>
    public enum TextWhiteSpace
    {
        /// <inheritdoc cref="TextSeparator.CommonNumberSeparator"/>
        CommonNumberSeparator,

        /// <inheritdoc cref="TextSeparator.HorizontalTab"/>
        HorizontalTab,

        /// <inheritdoc cref="TextSeparator.Space"/>
        Space,

        /// <inheritdoc cref="TextSeparator.BoundaryNeutral"/>
        BoundaryNeutral,

        /// <inheritdoc cref="TextSeparator.CarriageReturn"/>
        CarriageReturn,

        /// <inheritdoc cref="TextSeparator.FormFeed"/>
        FormFeed,

        /// <inheritdoc cref="TextSeparator.LineFeed"/>
        LineFeed,

        /// <inheritdoc cref="TextSeparator.LineSeparator"/>
        LineSeparator,

        /// <inheritdoc cref="TextSeparator.NextLine"/>
        NextLine,

        /// <inheritdoc cref="TextSeparator.ParagraphSeparator"/>
        ParagraphSeparator,

        /// <inheritdoc cref="TextSeparator.VerticalTab"/>
        VerticalTab,

        /// <inheritdoc cref="TextSeparator.WindowsDefault"/>
        WindowsDefault
    }

    /// <summary>
    ///     Provides constant <see cref="string"/> values of separator characters.
    /// </summary>
    public static class TextSeparator
    {
        /// <summary>
        ///     Boundary Neutral [BN].
        /// </summary>
        public const string BoundaryNeutral = "\u200B";

        /// <inheritdoc cref="BoundaryNeutral"/>
        public const char BoundaryNeutralChar = '\u200B';

        /// <summary>
        ///     Carriage Return [CR].
        /// </summary>
        public const string CarriageReturn = "\r";

        /// <inheritdoc cref="CarriageReturn"/>
        public const char CarriageReturnChar = '\r';

        /// <summary>
        ///     Common Number Separator [CS].
        /// </summary>
        public const string CommonNumberSeparator = "\u00a0";

        /// <inheritdoc cref="CommonNumberSeparator"/>
        public const char CommonNumberSeparatorChar = '\u00a0';

        /// <summary>
        ///     Form Feed [FF].
        /// </summary>
        public const string FormFeed = "\f";

        /// <inheritdoc cref="FormFeed"/>
        public const char FormFeedChar = '\f';

        /// <summary>
        ///     Horizontal Tab [TAB].
        /// </summary>
        public const string HorizontalTab = "\t";

        /// <inheritdoc cref="HorizontalTab"/>
        public const char HorizontalTabChar = '\t';

        /// <summary>
        ///     Line Feed [LF].
        /// </summary>
        public const string LineFeed = "\n";

        /// <inheritdoc cref="LineFeed"/>
        public const char LineFeedChar = '\n';

        /// <summary>
        ///     Line Separator.
        /// </summary>
        public const string LineSeparator = "\u2028";

        /// <inheritdoc cref="LineSeparator"/>
        public const char LineSeparatorChar = '\u2028';

        /// <summary>
        ///     Next Line [NEL].
        /// </summary>
        public const string NextLine = "\u0085";

        /// <inheritdoc cref="NextLine"/>
        public const char NextLineChar = '\u0085';

        /// <summary>
        ///     Paragraph Separator [B].
        /// </summary>
        public const string ParagraphSeparator = "\u2029";

        /// <inheritdoc cref="ParagraphSeparator"/>
        public const char ParagraphSeparatorChar = '\u2029';

        /// <summary>
        ///     Space.
        /// </summary>
        public const string Space = " ";

        /// <inheritdoc cref="Space"/>
        public const char SpaceChar = ' ';

        /// <summary>
        ///     Vertical Tab [VT].
        /// </summary>
        public const string VerticalTab = "\v";

        /// <inheritdoc cref="VerticalTab"/>
        public const char VerticalTabChar = '\v';

        /// <summary>
        ///     Carriage Return [CR] &amp; Line Feed [LF].
        /// </summary>
        public const string WindowsDefault = "\r\n";

        private static readonly string[] AllNewLineStrs =
        {
            LineFeed,
            VerticalTab,
            FormFeed,
            CarriageReturn,
            NextLine,
            BoundaryNeutral,
            LineSeparator,
            ParagraphSeparator
        };

        private static readonly char[] AllNewLineChrs =
        {
            LineFeedChar,
            VerticalTabChar,
            FormFeedChar,
            CarriageReturnChar,
            NextLineChar,
            BoundaryNeutralChar,
            LineSeparatorChar,
            ParagraphSeparatorChar
        };

        private static readonly string[] AllWhiteSpaceStrs =
        {
            LineFeed,
            HorizontalTab,
            VerticalTab,
            FormFeed,
            CarriageReturn,
            Space,
            NextLine,
            CommonNumberSeparator,
            BoundaryNeutral,
            LineSeparator,
            ParagraphSeparator
        };

        private static readonly char[] AllWhiteSpaceChrs =
        {
            LineFeedChar,
            HorizontalTabChar,
            VerticalTabChar,
            FormFeedChar,
            CarriageReturnChar,
            SpaceChar,
            NextLineChar,
            CommonNumberSeparatorChar,
            BoundaryNeutralChar,
            LineSeparatorChar,
            ParagraphSeparatorChar
        };

        /// <summary>
        ///     Returns a sequence of all line separator strings.
        /// </summary>
        public static ReadOnlySpan<string> AllNewLineStrings => AllNewLineStrs;

        /// <summary>
        ///     Returns a sequence of all line separator characters.
        /// </summary>
        public static ReadOnlySpan<char> AllNewLineChars => AllNewLineChrs;

        /// <summary>
        ///     Returns a sequence of all whitespace strings.
        /// </summary>
        public static ReadOnlySpan<string> AllWhiteSpaceStrings => AllWhiteSpaceStrs;

        /// <summary>
        ///     Returns a sequence of all whitespace characters.
        /// </summary>
        public static ReadOnlySpan<char> AllWhiteSpaceChars => AllWhiteSpaceChrs;

        /// <summary>
        ///     Retrieves the characters of the specified separator.
        /// </summary>
        /// <param name="separator">
        ///     The value that determines the separator.
        /// </param>
        /// <returns>
        ///     A string that represents a line separator character.
        /// </returns>
        public static string GetSeparatorStr(TextNewLine separator) =>
            separator switch
            {
                TextNewLine.BoundaryNeutral => BoundaryNeutral,
                TextNewLine.CarriageReturn => CarriageReturn,
                TextNewLine.FormFeed => FormFeed,
                TextNewLine.LineFeed => LineFeed,
                TextNewLine.LineSeparator => LineSeparator,
                TextNewLine.NextLine => NextLine,
                TextNewLine.ParagraphSeparator => ParagraphSeparator,
                TextNewLine.VerticalTab => VerticalTab,
                TextNewLine.WindowsDefault => WindowsDefault,
                _ => throw new ArgumentOutOfRangeException(nameof(separator))
            };

        /// <returns>
        ///     A string that represents a whitespace character.
        /// </returns>
        /// <inheritdoc cref="GetSeparatorStr(TextNewLine)"/>
        public static string GetSeparatorStr(TextWhiteSpace separator) =>
            separator switch
            {
                TextWhiteSpace.CommonNumberSeparator => CommonNumberSeparator,
                TextWhiteSpace.HorizontalTab => HorizontalTab,
                TextWhiteSpace.Space => Space,
                _ => GetSeparatorStr((TextNewLine)separator)
            };

        /// <summary>
        ///     Retrieves the character of the specified separator.
        /// </summary>
        /// <param name="separator">
        ///     The value that determines the separator.
        /// </param>
        /// <exception cref="NotSupportedException">
        ///     separator is <see cref="TextNewLine.WindowsDefault"/>, which cannot be a
        ///     single character.
        /// </exception>
        /// <returns>
        ///     A character that represents a line separator character.
        /// </returns>
        public static char GetSeparatorChar(TextNewLine separator) =>
            separator switch
            {
                TextNewLine.BoundaryNeutral => BoundaryNeutralChar,
                TextNewLine.CarriageReturn => CarriageReturnChar,
                TextNewLine.FormFeed => FormFeedChar,
                TextNewLine.LineFeed => LineFeedChar,
                TextNewLine.LineSeparator => LineSeparatorChar,
                TextNewLine.NextLine => NextLineChar,
                TextNewLine.ParagraphSeparator => ParagraphSeparatorChar,
                TextNewLine.VerticalTab => VerticalTabChar,
                TextNewLine.WindowsDefault => throw new NotSupportedException(),
                _ => throw new ArgumentOutOfRangeException(nameof(separator))
            };

        /// <exception cref="NotSupportedException">
        ///     separator is <see cref="TextWhiteSpace.WindowsDefault"/>, which cannot be a
        ///     single character.
        /// </exception>
        /// <returns>
        ///     A character that represents a whitespace character.
        /// </returns>
        /// <inheritdoc cref="GetSeparatorChar(TextNewLine)"/>
        public static char GetSeparatorChar(TextWhiteSpace separator) =>
            separator switch
            {
                TextWhiteSpace.CommonNumberSeparator => CommonNumberSeparatorChar,
                TextWhiteSpace.HorizontalTab => HorizontalTabChar,
                TextWhiteSpace.Space => SpaceChar,
                TextWhiteSpace.WindowsDefault => throw new NotSupportedException(),
                _ => GetSeparatorChar((TextNewLine)separator)
            };

        /// <summary>
        ///     Retrieves the characters of the specified separator.
        /// </summary>
        /// <param name="separator">
        ///     The value that determines the separator.
        /// </param>
        /// <returns>
        ///     A sequence of characters that represent a line separator character.
        /// </returns>
        public static ReadOnlySpan<char> GetSeparator(TextNewLine separator) =>
            GetSeparatorStr(separator);

        /// <returns>
        ///     A sequence of characters that represent a whitespace character.
        /// </returns>
        /// <inheritdoc cref="GetSeparator(TextNewLine)"/>
        public static ReadOnlySpan<char> GetSeparator(TextWhiteSpace separator) =>
            GetSeparatorStr(separator);
    }
}
