namespace Roydl.Text.Test.BinaryToTextTests
{
    using System;
    using System.IO;
    using System.Text;
    using BinaryToText;
    using NUnit.Framework;

    [TestFixture]
    [NonParallelizable]
    [Platform(Include = TestVars.PlatformInclude)]
    public class Base10Tests
    {
        private const BinToTextEncoding Algorithm = BinToTextEncoding.Base10;
        private const string ExpectedTestEncoded = "084101115116";
        private const string ExpectedRangeEncoded = "000001002003004005006007008009010011012013014015016017018019020021022023024025026027028029030031032033034035036037038039040041042043044045046047048049050051052053054055056057058059060061062063064065066067068069070071072073074075076077078079080081082083084085086087088089090091092093094095096097098099100101102103104105106107108109110111112113114115116117118119120121122123124125126127194128194129194130194131194132194133194134194135194136194137194138194139194140194141194142194143194144194145194146194147194148194149194150194151194152194153194154194155194156194157194158194159194160194161194162194163194164194165194166194167194168194169194170194171194172194173194174194175194176194177194178194179194180194181194182194183194184194185194186194187194188194189194190194191195128195129195130195131195132195133195134195135195136195137195138195139195140195141195142195143195144195145195146195147195148195149195150195151195152195153195154195155195156195157195158195159195160195161195162195163195164195165195166195167195168195169195170195171195172195173195174195175195176195177195178195179195180195181195182195183195184195185195186195187195188195189195190";
        private static readonly string TestFileSrcPath = TestVars.GetTempFilePath();
        private static readonly string TestFileDestPath = TestVars.GetTempFilePath();

        private static readonly TestCaseData[] TestData =
        {
            new(TestVarsType.TestStream, ExpectedTestEncoded),
            new(TestVarsType.TestBytes, ExpectedTestEncoded),
            new(TestVarsType.TestString, ExpectedTestEncoded),
            new(TestVarsType.TestFile, ExpectedTestEncoded),
            new(TestVarsType.RangeString, ExpectedRangeEncoded)
        };

        private static Base10 _instance;

        [OneTimeSetUp]
        public void CreateInstanceAndTestFile()
        {
            _instance = new Base10();
            File.WriteAllText(TestFileSrcPath, TestVars.TestStr);
        }

        [OneTimeTearDown]
        public void CleanUpTestFile()
        {
            if (File.Exists(TestFileSrcPath))
                File.Delete(TestFileSrcPath);
            if (File.Exists(TestFileDestPath))
                File.Delete(TestFileDestPath);
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(varsType), varsType, null);
            }
            Assert.AreEqual(expectedEncoded, encoded);
            Assert.AreEqual(original, decoded);
        }

        [Test]
        [Category("New")]
        public void InstanceCtor()
        {
            var instance = new Base10();
            Assert.IsInstanceOf(typeof(Base10), instance);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), instance);
            Assert.AreNotSame(_instance, instance);

            var defaultInstance1 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base10), defaultInstance1);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), defaultInstance1);
            Assert.AreNotSame(instance, defaultInstance1);

            var defaultInstance2 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base10), defaultInstance2);
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(varsType), varsType, null);
            }
            Assert.AreEqual(expectedEncoded, encoded);
            Assert.AreEqual(original, decoded);
        }
    }
}
