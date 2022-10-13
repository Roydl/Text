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
    public class Base32Tests
    {
        private const BinToTextEncoding Algorithm = BinToTextEncoding.Base32;
        private const string ExpectedTestEncoded = "KRSXG5A=";
        private const string ExpectedRangeEncoded = "AAAQEAYEAUDAOCAJBIFQYDIOB4IBCEQTCQKRMFYYDENBWHA5DYPSAIJCEMSCKJRHFAUSUKZMFUXC6MBRGIZTINJWG44DSOR3HQ6T4P2AIFBEGRCFIZDUQSKKJNGE2TSPKBIVEU2UKVLFOWCZLJNVYXK6L5QGCYTDMRSWMZ3INFVGW3DNNZXXA4LSON2HK5TXPB4XU634PV7H7QUAYKA4FAWCQPBIJQUFYKDMFB6CRDBITQUKYKF4FDGCRXBI5QUPYKIMFEOCSLBJHQUUYKK4FFWCS7BJRQUZYKNMFG6CTTBJ3QU6YKP4FIGCUHBKFQVDYKSMFJOCU3BKPQVIYKU4FKWCVPBKZQVNYKXMFL6CWDBLDQVSYKZ4FNGCWXBLNQVXYK4MFOOCXLBLXQV4YK64FPWCX7BYBQ4BYOBMHA6DQTBYLQ4GYOD4HCGDRHBYVQ4LYOGMHDODR3BY7Q4QYOI4HEWDSPBZJQ4VYOLMHF6DTDBZTQ42YON4HHGDTXBZ5Q47YOQMHIODULB2HQ5EYOS4HJWDU7B2RQ5JYOVMHK6DVTB23Q5OYOX4HMGDWHB3FQ5TYO2MHNODW3B3PQ5YYO44HOWDXPB3ZQ55YO7A====";
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

        private static Base32 _instance;

        [OneTimeSetUp]
        public void CreateInstanceAndTestFile()
        {
            _instance = new Base32();
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
            var instance = new Base32();
            Assert.IsInstanceOf(typeof(Base32), instance);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), instance);
            Assert.AreNotSame(_instance, instance);

            var defaultInstance1 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base32), defaultInstance1);
            Assert.IsInstanceOf(typeof(BinaryToTextEncoding), defaultInstance1);
            Assert.AreNotSame(instance, defaultInstance1);

            var defaultInstance2 = Algorithm.GetDefaultInstance();
            Assert.IsInstanceOf(typeof(Base32), defaultInstance2);
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
