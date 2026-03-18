<p align="center">
<a href="https://dotnet.microsoft.com/download/dotnet/10.0" rel="nofollow"><img src="https://img.shields.io/badge/Core%2010%20LTS-or%20higher-lightgrey?style=for-the-badge&logo=dotnet&logoColor=white" title=".NET10 LTS or higher" alt=".NET Core"></a>
<a href="https://github.com/Roydl/Text/actions"><img src="https://img.shields.io/badge/cross%E2%80%93platform-%e2%9c%94-blue?style=for-the-badge&logo=linux&logoColor=silver" title="Automatically tested with Windows 11 &amp; Ubuntu 24.04 LTS" alt="Cross-platform"></a>
<a href="https://github.com/Roydl/Text/blob/master/LICENSE.txt"><img src="https://img.shields.io/github/license/Roydl/Text?style=for-the-badge" title="Read the license terms" alt="License"></a>
</p>
<p align="center">
<a href="https://github.com/Roydl/Text/actions/workflows/dotnet.yml"><img src="https://img.shields.io/github/actions/workflow/status/Roydl/Text/dotnet.yml?label=build%2Btest&logo=github&logoColor=silver&style=for-the-badge" title="Check the last workflow results" alt="Build+Test"></a>
<a href="https://github.com/Roydl/Text/commits/master"><img src="https://img.shields.io/github/last-commit/Roydl/Text?style=for-the-badge&logo=github&logoColor=silver" title="Check the last commits" alt="Commits"></a>
<a href="https://github.com/Roydl/Text/archive/refs/heads/master.zip"><img src="https://img.shields.io/badge/download-source-important?style=for-the-badge&logo=github&logoColor=silver" title="Start downloading the &apos;master.zip&apos; file" alt="Source"></a>
</p>
<p align="center">
<a href="https://www.nuget.org/packages/Roydl.Text"><img src="https://img.shields.io/nuget/v/Roydl.Text?style=for-the-badge&logo=nuget&logoColor=silver&label=nuget" title="Check out the NuGet package page" alt="NuGet"></a>
<a href="https://www.nuget.org/packages/Roydl.Text"><img src="https://img.shields.io/nuget/dt/Roydl.Text?logo=nuget&logoColor=silver&style=for-the-badge" title="Check out the NuGet package page" alt="NuGet"></a>
<a href="https://www.si13n7.com"><img src="https://img.shields.io/website/https/www.si13n7.com?style=for-the-badge&down_color=critical&down_message=down&up_color=success&up_message=up&logo=data%3Aimage%2Fpng%3Bbase64%2CiVBORw0KGgoAAAANSUhEUgAAAA4AAAAOCAYAAAAfSC3RAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAEwSURBVDhPxZJNSgNBEIXnCp5AcCO4CmaTRRaKBhdCFkGCCKLgz2Y2RiQgCiqZzmi3CG4COj0X8ApewSt4Ba%2FQ9leZGpyVG8GComtq3qv3qmeS%2Fw9nikHMd5sVn3bqLx7zom1NcW8z%2F6G9CjoPm722rPEv45EJ21vD0O30AvX12IWDvTRsrPXrnjPlUYO0u3McVpZXhch5cnguZ7vVDWfpjRAZgPqc%2BIMEgKQe9Pfr0xn%2FBqZJjAUNQKilp5cC1gHYYz8Usc3OQsTz9HZWK5BMJwFDwrbWbuIXhfhg%2FDpWuE2mK5lEgQtiz4baU14u3V09i5peiipy6qVAxFWtZiflJiq8AAiIZx1CnxpStGmEpEHDZf4r2pUd%2BMjYxomoxJofo4L%2FHqyR57OF6vEvIkm%2BAYRc%2BWd4P97CAAAAAElFTkSuQmCC" title="Visit the developer&apos;s website" alt="Website"></a>
<a href="https://www.si13n7.de"><img src="https://img.shields.io/website/https/www.si13n7.de?style=for-the-badge&down_color=critical&down_message=down&label=mirror&up_color=success&up_message=up&logo=data%3Aimage%2Fpng%3Bbase64%2CiVBORw0KGgoAAAANSUhEUgAAAA4AAAAOCAYAAAAfSC3RAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAEwSURBVDhPxZJNSgNBEIXnCp5AcCO4CmaTRRaKBhdCFkGCCKLgz2Y2RiQgCiqZzmi3CG4COj0X8ApewSt4Ba%2FQ9leZGpyVG8GComtq3qv3qmeS%2Fw9nikHMd5sVn3bqLx7zom1NcW8z%2F6G9CjoPm722rPEv45EJ21vD0O30AvX12IWDvTRsrPXrnjPlUYO0u3McVpZXhch5cnguZ7vVDWfpjRAZgPqc%2BIMEgKQe9Pfr0xn%2FBqZJjAUNQKilp5cC1gHYYz8Usc3OQsTz9HZWK5BMJwFDwrbWbuIXhfhg%2FDpWuE2mK5lEgQtiz4baU14u3V09i5peiipy6qVAxFWtZiflJiq8AAiIZx1CnxpStGmEpEHDZf4r2pUd%2BMjYxomoxJofo4L%2FHqyR57OF6vEvIkm%2BAYRc%2BWd4P97CAAAAAElFTkSuQmCC" title="Visit the developer&apos;s mirror website" alt="Mirror"></a>
</p>

# Roydl.Text

Roydl.Text provides a simple, generic way to encode and decode binary data as text. Extension methods are available for `string` and `byte[]`, and a growing set of encodings is offered — all of which are performance-optimized and parallelized across available CPU cores, with AVX2 and AVX-512 SIMD acceleration where applicable.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Install](#install)
- [Binary-To-Text Encodings](#binary-to-text-encodings)
- [Encoding Performance](#encoding-performance)
- [Usage](#usage)
- [Would you like to help?](#would-you-like-to-help)

---

## Prerequisites

- [.NET 10 LTS](https://dotnet.microsoft.com/download/dotnet/10.0) or higher
- Supported platforms: Windows, Linux, macOS
- Hardware acceleration (optional): AVX2 or AVX-512 capable CPU

---

## Install
```
$ dotnet add package Roydl.Text
```

---

## Binary-To-Text Encodings

| Type | Character Set | Output Ratio | Hardware Support |
| :---- | :---- | ----: | :----: |
| Base-2 | `0` and `1` | 8× | AVX-512BW<br>AVX2 |
| Base-8 | `0–7` | 3× | AVX-512BW<br>AVX2 |
| Base-10 | `0–9` | 3× | AVX-512BW<br>AVX2 |
| Base-16 | `0–9` and `a–f` | 2× | AVX-512BW<br>AVX2 |
| Base-32 | `A–Z` and `2–7`; `=` for padding | 1.6× | AVX2 ¹ |
| Base-64 | `A–Z`, `a–z`, `0–9`, `+` and `/`; `=` for padding | 1.33× | AVX2 ² |
| Base-85 | ASCII printable range `!`–`u`; `z` shortcut for null groups | 1.25× | AVX2 |
| Base-91 | `A–Z`, `a–z`, `0–9` and <code>!#$%&()*+,-.:;<=>?@[]^_`{&#124;}~"</code> | ~1.23× | None ³ |

> ¹ AVX2 is used for the alphabet lookup phase only. The non-power-of-two 5-bit group width prevents full SIMD vectorization of the bit-extraction phase.
>
> ² Delegates to .NET's built-in `System.Buffers.Text.Base64` which is internally AVX2-accelerated. Parallelization and double-buffered I/O are layered on top.
>
> ³ The algorithm maintains a serial bit-accumulator state across every byte, making it fundamentally incompatible with SIMD vectorization or parallel processing. Any optimization that would break this dependency chain would also break compatibility with existing encoded data.

> For general binary-to-text encoding, Base-85 and Base-91 offer better compactness than Base-64 — Base-85 produces ~6% smaller output, and Base-91 ~9% smaller. Base-85 is the better practical choice of the two: it is over 7× faster than Base-91 while sacrificing only marginal compactness.

---

## Encoding Performance

_Base-64 and Base-16 are the fastest encodings in this library. Base-91 is a known outlier — its serial design makes parallelization impossible without breaking the algorithm._

| Encoding | Throughput |
| :---- | ----: |
| Base-2 | **2.2 GiB/s** |
| Base-8 | **1.0 GiB/s** |
| Base-10 | **1.2 GiB/s** |
| Base-16 | **7.5 GiB/s** |
| Base-32 | **1.3 GiB/s** |
| Base-64 | **9.6 GiB/s** |
| Base-85 | **2.8 GiB/s** |
| Base-91 | **380 MiB/s** |

<details>
<summary>Benchmark methodology</summary>

| Component | Details |
| :--- | :--- |
| CPU | AMD Ryzen 5 7600 (6C/12T, 5.1 GHz boost) |
| RAM | 32 GB DDR5 |
| OS | Manjaro Linux (Kernel 6.19.2-1) |
| Runtime | .NET 10 |
| Build | Release (`dotnet run -c Release`) |

Each encoding is benchmarked using stream reuse to eliminate allocation overhead. Four input patterns are tested per encoding: random bytes, all-zeros, sequential, and mixed (25% zero groups). Each pattern runs five cycles of three seconds each. The reported throughput is the median across all patterns and cycles, which avoids cache-warmup bias and reflects sustained real-world performance. You can find the benchmark test [here](https://github.com/Roydl/Text/blob/master/test/BenchmarkTests/BinToTextPerformanceTests.cs#L98).

</details>

---

## Usage
```cs
// Encode — value can be string or byte[]
// BinToTextEncoding defaults to Base64 if not specified
string encoded = value.Encode(BinToTextEncoding.Base85);

// Decode
byte[] original = encoded.Decode(BinToTextEncoding.Base85);
string original = encoded.DecodeString(BinToTextEncoding.Base85);

// File encoding via extension methods
// For large files, use the instance-based approach below instead
string encoded = path.EncodeFile(BinToTextEncoding.Base85);
byte[] original = path.DecodeFile(BinToTextEncoding.Base85);

// Instance-based — recommended for large files or repeated use
// GetDefaultInstance() returns a cached singleton per encoding type
var encoder = BinToTextEncoding.Base85.GetDefaultInstance();

// Stream-based — most efficient for large files
using var input = new FileStream(srcPath, FileMode.Open, FileAccess.Read);
using var output = new FileStream(destPath, FileMode.Create);
encoder.EncodeStream(input, output);

// Line length — inserts Environment.NewLine after every N encoded chars
string encoded = value.Encode(BinToTextEncoding.Base64, lineLength: 76);

// All public methods are available on every encoding instance
string encoded  = encoder.EncodeBytes(bytes);
string encoded  = encoder.EncodeString(text);
string encoded  = encoder.EncodeFile(path);
byte[] original = encoder.DecodeBytes(encoded);
string original = encoder.DecodeString(encoded);
byte[] original = encoder.DecodeFile(path);
```

---

## Would you like to help?

- [Star this Project](https://github.com/Roydl/Text/stargazers) :star: and show me that this project interests you :hugs:
- [Open an Issue](https://github.com/Roydl/Text/issues/new) :coffee: to give me your feedback and tell me your ideas and wishes for the future :sunglasses:
- [Open a Ticket](https://www.si13n7.com/?page=contact) :mailbox: if you don't have a GitHub account, you can contact me directly on my website :wink:
- [Donate by PayPal](https://paypal.me/si13n7/) :money_with_wings: to buy me some cakes :cake:
