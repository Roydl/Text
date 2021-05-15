namespace Roydl.Text.Test.BinaryToTextTests
{
    using System;
    using System.IO;
    using System.Text;
    using BinaryToText;
    using NUnit.Framework;

    [TestFixture]
    [Parallelizable]
    [Platform(Include = TestVars.PlatformInclude)]
    public class Base08Tests
    {
        private const BinToTextEncoding Algorithm = BinToTextEncoding.Base08;
        private const string ExpectedTestEncoded = "124145163164";
        private const string ExpectedRangeEncoded = "000001002003004005006007010011012013014015016017020021022023024025026027030031032033034035036037040041042043044045046047050051052053054055056057060061062063064065066067070071072073074075076077100101102103104105106107110111112113114115116117120121122123124125126127130131132133134135136137140141142143144145146147150151152153154155156157160161162163164165166167170171172173174175176177302200302201302202302203302204302205302206302207302210302211302212302213302214302215302216302217302220302221302222302223302224302225302226302227302230302231302232302233302234302235302236302237302240302241302242302243302244302245302246302247302250302251302252302253302254302255302256302257302260302261302262302263302264302265302266302267302270302271302272302273302274302275302276302277303200303201303202303203303204303205303206303207303210303211303212303213303214303215303216303217303220303221303222303223303224303225303226303227303230303231303232303233303234303235303236303237303240303241303242303243303244303245303246303247303250303251303252303253303254303255303256303257303260303261303262303263303264303265303266303267303270303271303272303273303274303275303276";
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

        private static Base08 _instance;

        [OneTimeSetUp]
        public void CreateInstanceAndTestFile()
        {
            _instance = new Base08();
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
            var instance = new Base08();
            Assert.IsInstanceOf(typeof(Base08), instance);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), instance);
            Assert.AreNotSame(_instance, instance);

            var defaultInstance1 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base08), defaultInstance1);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), defaultInstance1);
            Assert.AreNotSame(instance, defaultInstance1);

            var defaultInstance2 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base08), defaultInstance2);
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
