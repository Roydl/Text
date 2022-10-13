<p align="center">
<a href="https://dotnet.microsoft.com/download/dotnet/5.0" rel="nofollow"><img src="https://img.shields.io/badge/core-v3.1%20or%20higher-lightgrey?style=for-the-badge&logo=dot-net&logoColor=white" title=".NET Core v3.1 LTS or higher" alt=".NET Core"></a>
<a href="https://github.com/Roydl/Text/actions"><img src="https://img.shields.io/badge/cross%E2%80%93platform-%e2%9c%94-blue?style=for-the-badge&logo=linux&logoColor=silver" title="Automatically tested with Windows 10 &amp; Ubuntu 20.04 LTS" alt="Cross-platform"></a>
<a href="https://github.com/Roydl/Text/blob/master/LICENSE.txt"><img src="https://img.shields.io/github/license/Roydl/Text?style=for-the-badge" title="Read the license terms" alt="License"></a>
</p>
<p align="center">
<a href="https://github.com/Roydl/Text/actions/workflows/dotnet.yml"><img src="https://img.shields.io/github/workflow/status/Roydl/Text/build%2Btest?style=for-the-badge&label=build%2Btest&logo=github&logoColor=silver" title="Check the last workflow results" alt="Build+Test"></a>
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

The idea was to create a comfortable way of binary-to-text encoding.

You can easily create instances of any type to translate `Stream`, `byte[]` or `string` data. Extension methods are also provided for all types.


## Binary-To-Text Encoding

| Type | Encoding |
| ---- | ---- |
| Base-2 | Binary character set: `0` and `1` |
| Base-8 | Octal character set: `0-7` |
| Base-10 | Decimal character set: `0-9` |
| Base-16 | Hexadecimal character set: `0-9` and `a-f` |
| Base-32 | Standard 32-character set: `A–Z` and `2–7`; `=` for padding |
| Base-64 | Standard 64-character set: `A–Z`, `a–z`, `0–9`, `+` and `/`; `=` for padding |
| Base-85 | Standard 85-character set: `!"#$%&'()*+,-./`, `0-9`, `:;<=>?@`, `A-Z`, <code>[]^_&#96;</code> and `a-u` |
| Base-91 | Standard 91-character set: `A–Z`, `a–z`, `0–9`, and <code>!&#35;$%&amp;()*+,-.:;&lt;=&gt;?@[]^_&#96;{&#124;}~&quot;</code> |


### Usage:
```cs
// The `value` must be type `string` or `byte[]`, if `BinToTextEncoding` is
// not set, `Base64` is used by default.
string base85text = value.Encode(BinToTextEncoding.Base85);
byte[] original = value.Decode(BinToTextEncoding.Base85); // if `value` to decode is `byte[]`
string original = value.DecodeString(BinToTextEncoding.Base85); // if `value` to decode is `string`

// The `value` of type `string` can also be a file path, which is not
// recommended for large files, in this case you should create a
// `Base85` instance and use `FileStream` to read and write.
string base85text = value.EncodeFile(BinToTextEncoding.Base85);
byte[] original = value.DecodeFile(BinToTextEncoding.Base85);
```


---


## Would you like to help?

- [Star this Project](https://github.com/Roydl/Text/stargazers) :star: and show me that this project interests you :hugs:
- [Open an Issue](https://github.com/Roydl/Text/issues/new) :coffee: to give me your feedback and tell me your ideas and wishes for the future :sunglasses:
- [Open a Ticket](https://www.si13n7.com/?page=contact) :mailbox: if you don't have a GitHub account, you can contact me directly on my website :wink:
- [Donate by PayPal](https://paypal.me/si13n7/) :money_with_wings: to buy me some cakes :cake:

_Please note that I cannot fix bugs that are unknown to me. So do yourself and me the favor and get in touch with me._ :face_with_head_bandage:
