namespace Roydl.Text.Test
{
    using System;
    using System.Linq;
    using NUnit.Framework;

    public class OtherTests
    {
        private static readonly TestCaseData[] Rot13TestData =
        {
            new(TestVarsType.TestString, "Grfg"),
            new(TestVarsType.QuoteString, "Jr xabj jung jr ner, ohg xabj abg jung jr znl or."),
            new(TestVarsType.RangeString, null),
        };

        [Test]
        [TestCaseSource(nameof(Rot13TestData))]
        public void Rot13(TestVarsType varsType, string expectedRotated)
        {
            string original;
            switch (varsType)
            {
                case TestVarsType.TestString:
                    original = TestVars.TestStr;
                    break;
                case TestVarsType.QuoteString:
                    original = TestVars.QuoteStr;
                    break;
                case TestVarsType.RangeString:
                    original = TestVars.RangeStr;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(varsType), varsType, null);
            }
            var rotated = TextConvert.Rot13(original);
            if (varsType != TestVarsType.RangeString)
                Assert.AreEqual(expectedRotated, rotated);
            var normalized = TextConvert.Rot13(rotated);
            Assert.AreEqual(original, normalized);
        }

        [Test]
        [Description("***WIP: Test not strong enough yet!")]
        public void FormatSeparators()
        {
            var original = $"{TextSeparator.HorizontalTab}{TextSeparator.HorizontalTab}{TextSeparator.HorizontalTab}{TestVars.QuoteStr}";
            foreach (var str in TextSeparator.AllNewLineStrings)
            {
                original += str;
                original += TextSeparator.WindowsDefault;
            }
            for (var i = 0; i < 3; i++)
                original += original;

            var allCount = original.Count(TextVerify.IsLineSeparator);
            var formatted = TextConvert.FormatSeparators(original, TextNewLine.LineFeed);
            Assert.AreEqual(allCount - 72, formatted.Count(TextVerify.IsLineSeparator));

            var lineFeedCount = formatted.Count(c => c == TextSeparator.LineFeedChar);
            var normalized = TextConvert.FormatSeparators(formatted);
            Assert.AreEqual(formatted.Length + lineFeedCount, normalized.Length);
        }
    }
}
