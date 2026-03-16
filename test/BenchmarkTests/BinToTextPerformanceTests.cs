#if RELEASE
namespace Roydl.Text.Test.BenchmarkTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using BinaryToText;
    using Microsoft.Win32;
    using NUnit.Framework;

    [TestFixture]
    [NonParallelizable]
    [Platform(Include = TestVars.PlatformCross)]
    [Category("Performance")]
    public class BinToTextPerformanceTests
    {
        private const int BenchmarkRepeats = 20;

        private static readonly TestCaseData[] BenchmarkTestData =
        [
            new(BinToTextEncoding.Base02, 65536),
            new(BinToTextEncoding.Base08, 65536),
            new(BinToTextEncoding.Base10, 65536),
            new(BinToTextEncoding.Base16, 65536),
            new(BinToTextEncoding.Base32, 65536),
            new(BinToTextEncoding.Base64, 65536),
            new(BinToTextEncoding.Base85, 65536),
            new(BinToTextEncoding.Base91, 65536)
        ];

        private static readonly ConcurrentDictionary<string, ConcurrentBag<double>> BenchmarkResults = new(Environment.ProcessorCount, BenchmarkTestData.Length);

        [Test]
        [Order(0)]
        public void PrintHardwareInfo()
        {
            string cpu = null;
            if (OperatingSystem.IsLinux())
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                        continue;
                    cpu = line[(line.IndexOf(':') + 2)..].Trim();
                    break;
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name)
                    cpu = name.Trim();
            }
            else
                cpu = Environment.MachineName;

            var cores = Environment.ProcessorCount;
            var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024;
            TestContext.WriteLine($"CPU:   {cpu}" +
                                  Environment.NewLine +
                                  $"Cores: {cores}" +
                                  Environment.NewLine +
                                  $"RAM:   {ram} GB" +
                                  Environment.NewLine);
        }

        [OneTimeTearDown]
        [SetCulture("en-US")]
        public void CreateResultFiles()
        {
            if (BenchmarkResults.IsEmpty)
                return;
            var dir = TestContext.CurrentContext.TestDirectory;
            Parallel.ForEach(BenchmarkResults, pair =>
            {
                var (key, value) = pair;
                var file = Path.Combine(dir, $"__Benchmark-{string.Concat(key.Split(Path.GetInvalidFileNameChars()))}.txt");
                var sorted = value.OrderByDescending(x => x).ToArray();
                var digits = BenchmarkRepeats.ToString(NumberFormatInfo.InvariantInfo).Length;
                var content =
                    $"Average: {sorted.Sum() / sorted.Length:0.0,6} MiB/s" +
                    Environment.NewLine +
                    $"   Best: {sorted[0]:0.0,6} MiB/s" +
                    Environment.NewLine +
                    $"  Worst: {sorted[^1]:0.0,6} MiB/s" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Results of {sorted.Length} runs with a total duration of {sorted.Length * 9} seconds:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, sorted.Select((x, i) => $"{(i + 1).ToString().PadLeft(digits)}: {x:0.0,6} MiB/s"));
                File.WriteAllText(file, content);
            });
        }

        private static void RunBenchmark(BinToTextEncoding algorithm, int packetSize, bool saveResults)
        {
            var inst = algorithm.GetDefaultInstance();
            var sw = TestVars.StopWatch;
            var patterns = Enum.GetValues<TestBytePattern>();

            const int cycles = 5;
            const int secondsPerCycle = 3;

            var patternRates = new double[patterns.Length];

            for (var pi = 0; pi < patterns.Length; pi++)
            {
                var data = TestVars.GetRandomBytes(packetSize, patterns[pi]);
                var rates = new double[cycles];

                // Pre-allocate output buffer sized for worst case (no 'z' groups)
                using var mso = new MemoryStream(data.Length / 4 * 5 + 4);

                // Warmup: one full cycle before measuring
                using (var msi = new MemoryStream(data))
                    inst.EncodeStream(msi, mso);

                for (var i = 0; i < cycles; i++)
                {
                    var total = 0L;
                    sw.Restart();
                    while (sw.Elapsed < TimeSpan.FromSeconds(secondsPerCycle))
                    {
                        // Reuse streams — avoids measuring MemoryStream allocation overhead
                        mso.Position = 0;
                        using var msi = new MemoryStream(data, false);
                        inst.EncodeStream(msi, mso);
                        total += data.Length;
                    }
                    sw.Stop();
                    rates[i] = total / sw.Elapsed.TotalSeconds / 1024 / 1024;
                }

                // Sort and take median — discards warm-cache outliers on both ends
                Array.Sort(rates);
                patternRates[pi] = rates[cycles / 2];
            }

            Array.Sort(patternRates);
            var rate = patternRates[patternRates.Length / 2];

            if (!saveResults)
            {
                TestContext.WriteLine($@"  {algorithm} Benchmark - Throughput: '{rate:0.0} MiB/s'; Packet Size: '{(packetSize > 1024 ? $"{packetSize / 1024:0} KiB" : $"{packetSize:0} Bytes")}';");
                TestContext.WriteLine(@"  Pattern Rates:");
                for (var pi = 0; pi < patterns.Length; pi++)
                    TestContext.WriteLine($@"  - {patterns[pi],-12} {patternRates[pi],8:0.0} MiB/s");
                TestContext.WriteLine();
                return;
            }

            var key = $"{algorithm}@{packetSize}";
            if (!BenchmarkResults.ContainsKey(key))
                BenchmarkResults[key] = [];
            BenchmarkResults[key].Add(rate);
        }

        [Test]
        [TestCaseSource(nameof(BenchmarkTestData))]
        public void BenchmarkOnce(BinToTextEncoding algorithm, int dataSize) =>
            RunBenchmark(algorithm, dataSize, false);

        [Explicit]
        [Test]
        [TestCaseSource(nameof(BenchmarkTestData))]
        [Repeat(BenchmarkRepeats)]
        public void BenchmarkRepeat(BinToTextEncoding algorithm, int dataSize) =>
            RunBenchmark(algorithm, dataSize, true);
    }
}
#endif
