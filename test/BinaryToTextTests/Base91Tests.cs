namespace Roydl.Text.Test.BinaryToTextTests
{
    using System;
    using System.IO;
    using System.Text;
    using BinaryToText;
    using NUnit.Framework;

    [TestFixture]
    [Parallelizable]
    [Platform(Include = TestVars.PlatformCross)]
    public class Base91Tests
    {
        private const BinToTextEncoding Algorithm = BinToTextEncoding.Base91;
        private const string ExpectedTestEncoded = "\"ONKd";
        private const string ExpectedRangeEncoded = ":C#(:C?hVB$MSiVEwndBAMZRxwFfBB;IW<}YQV!A_v$Y_c%zr4cYQPFl0,@heMAJ<:N[*T+.SFGr*`b4PD}vgYqU>cW0P*1NwV,O{cQ5u0m900[8@n4,wh?DP<2+~jQSW6nmLm1o-J,?jTs%2<WF%qb=oh|}EO6WrCFfk)GH!4EEDmT?yDvcowYe4_-ufO_Y*Ud|l)TH;5RNOVTi5DIdB1<&JR<u5OTbEnz1n)gH}6eWZEV?#D8d15jN4n?u^Otde5.Tp)tHK8rfm=Ui+DVeO!|xL!]uSP,f4;G<q)6HX94ox8W?<D.e&&w{5_6v$T;(@Dgq))[JZ?)E!rii}E:n-wIhRRuv\"TK+PW2I+)DKm@[N.ak?DFjoh18*#nxvZUk-po>$,)QKz[DX_JkiKF|o`5TQT!0vzU!:(6Jf.)dK$]QgG^l?QFwpu!,0%_3v=U}<C=h|:)qK=^dpR&liXFJqH(";
        private static readonly string TestFileSrcPath = TestVars.GetTempFilePath(Algorithm.ToString());
        private static readonly string TestFileDestPath = TestVars.GetTempFilePath(Algorithm.ToString());

        private static readonly TestCaseData[] TestData =
        {
            new(TestVarsType.TestStream, ExpectedTestEncoded),
            new(TestVarsType.TestBytes, ExpectedTestEncoded),
            new(TestVarsType.TestString, ExpectedTestEncoded),
            new(TestVarsType.TestFile, ExpectedTestEncoded),
            new(TestVarsType.RangeString, ExpectedRangeEncoded),
            new(TestVarsType.RandomBytes, null)
        };

        private static Base91 _instance;

        [OneTimeSetUp]
        public void CreateInstanceAndTestFile()
        {
            _instance = new Base91();
            File.WriteAllText(TestFileSrcPath, TestVars.TestStr);
        }

        [OneTimeTearDown]
        public void CleanUpTestFiles()
        {
            var dir = Path.GetDirectoryName(TestFileSrcPath);
            if (dir == null)
                return;
            foreach (var file in Directory.GetFiles(dir, $"test-{Algorithm}-*.tmp"))
                File.Delete(file);
        }

        [Test]
        [TestCaseSource(nameof(TestData))]
        [Category("Extension")]
        public void ExtensionEncodeDecode(TestVarsType varsType, string expectedEncoded)
        {
            object original, decoded;
            string encoded;
            switch (varsType)
            {
                case TestVarsType.TestStream:
                    // No extension for streams
                    return;
                case TestVarsType.TestBytes:
                    original = TestVars.TestBytes;
                    encoded = ((byte[])original).Encode(Algorithm);
                    decoded = encoded.Decode(Algorithm);
                    break;
                case TestVarsType.TestString:
                    original = TestVars.TestStr;
                    encoded = ((string)original).Encode(Algorithm);
                    decoded = encoded.DecodeString(Algorithm);
                    break;
                case TestVarsType.TestFile:
                    Assert.IsTrue(_instance.EncodeFile(TestFileSrcPath, TestFileDestPath));
                    original = TestVars.TestBytes;
                    encoded = TestFileSrcPath.EncodeFile(Algorithm);
                    decoded = TestFileDestPath.DecodeFile(Algorithm);
                    break;
                case TestVarsType.QuoteString:
                    original = TestVars.QuoteStr;
                    encoded = ((string)original).Encode(Algorithm);
                    decoded = encoded.DecodeString(Algorithm);
                    break;
                case TestVarsType.RangeString:
                    original = TestVars.RangeStr;
                    encoded = ((string)original).Encode(Algorithm);
                    decoded = encoded.DecodeString(Algorithm);
                    break;
                case TestVarsType.RandomBytes:
                    original = TestVars.GetRandomBytes();
                    encoded = ((byte[])original).Encode(Algorithm);
                    decoded = encoded.Decode(Algorithm);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(varsType), varsType, null);
            }
            if (expectedEncoded != null)
                Assert.AreEqual(expectedEncoded, encoded);
            Assert.AreEqual(original, decoded);
        }

        [Test]
        [Category("New")]
        public void InstanceCtor()
        {
            var instance = new Base91();
            Assert.IsInstanceOf(typeof(Base91), instance);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), instance);
            Assert.AreNotSame(_instance, instance);

            var defaultInstance1 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base91), defaultInstance1);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), defaultInstance1);
            Assert.AreNotSame(instance, defaultInstance1);

            var defaultInstance2 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base91), defaultInstance2);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), defaultInstance2);
            Assert.AreSame(defaultInstance1, defaultInstance2);
        }

        [Test]
        [TestCaseSource(nameof(TestData))]
        [Category("Method")]
        public void InstanceEncodeDecode(TestVarsType varsType, string expectedEncoded)
        {
            object original, decoded;
            string encoded;
            switch (varsType)
            {
                case TestVarsType.TestStream:
                    original = TestVars.TestBytes;

                    // dispose
                    using (var msi = new MemoryStream((byte[])original))
                    {
                        var mso = new MemoryStream();
                        _instance.EncodeStream(msi, mso, true);
                        try
                        {
                            msi.Position = 0L;
                        }
                        catch (Exception e)
                        {
                            Assert.AreEqual(typeof(ObjectDisposedException), e.GetType());
                        }
                        try
                        {
                            mso.Position = 0L;
                        }
                        catch (Exception e)
                        {
                            Assert.AreEqual(typeof(ObjectDisposedException), e.GetType());
                        }
                    }

                    // encode
                    using (var msi = new MemoryStream((byte[])original))
                    {
                        using var mso = new MemoryStream();
                        _instance.EncodeStream(msi, mso);
                        encoded = Encoding.UTF8.GetString(mso.ToArray());
                    }

                    // decode
                    using (var msi = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                    {
                        using var mso = new MemoryStream();
                        _instance.DecodeStream(msi, mso);
                        decoded = mso.ToArray();
                    }
                    break;
                case TestVarsType.TestBytes:
                    original = TestVars.TestBytes;
                    encoded = _instance.EncodeBytes((byte[])original);
                    decoded = _instance.DecodeBytes(encoded);
                    break;
                case TestVarsType.TestString:
                    original = TestVars.TestStr;
                    encoded = _instance.EncodeString((string)original);
                    decoded = _instance.DecodeString(encoded);
                    break;
                case TestVarsType.TestFile:
                    Assert.IsTrue(_instance.EncodeFile(TestFileSrcPath, TestFileDestPath));
                    original = TestVars.TestBytes;
                    encoded = _instance.EncodeFile(TestFileSrcPath);
                    decoded = _instance.DecodeFile(TestFileDestPath);
                    break;
                case TestVarsType.QuoteString:
                    original = TestVars.QuoteStr;
                    encoded = _instance.EncodeString((string)original);
                    decoded = _instance.DecodeString(encoded);
                    break;
                case TestVarsType.RangeString:
                    original = TestVars.RangeStr;
                    encoded = _instance.EncodeString((string)original);
                    decoded = _instance.DecodeString(encoded);
                    break;
                case TestVarsType.RandomBytes:
                    original = TestVars.GetRandomBytes();
                    encoded = _instance.EncodeBytes((byte[])original);
                    decoded = _instance.DecodeBytes(encoded);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(varsType), varsType, null);
            }
            if (expectedEncoded != null)
                Assert.AreEqual(expectedEncoded, encoded);
            Assert.AreEqual(original, decoded);
        }
    }
}
