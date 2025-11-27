# ZStack.MusicDecryptLib

一个用于解密各种音乐平台加密音乐文件的 .NET 库，支持网易云音乐（NCM）、酷狗音乐（KGM、KGG）等加密格式。

## ✨ 特性

- 🎵 支持多种音乐加密格式
  - **NCM** - 网易云音乐加密格式（.ncm）
  - **KGM** - 酷狗音乐加密格式（.kgm、.kgma）
  - **KGG** - 酷狗音乐加密格式（.kgg）
- 🚀 高性能异步解密
- 📦 支持流式处理，可处理大文件
- 🔧 可扩展的解密器架构，易于添加新格式支持

## 📦 安装

### NuGet Package Manager

```bash
Install-Package ZStack.MusicDecryptLib
```

### .NET CLI

```bash
dotnet add package ZStack.MusicDecryptLib
```

## 🚀 快速开始

### 基础用法 - 自动识别格式

```csharp
using ZStack.MusicDecryptLib;
using ZStack.MusicDecryptLib.Decrypters;

// 创建自动解密器
var autoDecrypter = new AutoDecrypter();

// 如果需要支持 KGG 格式，需要加载密钥数据库
autoDecrypter.AddKGG("path/to/kgmusic.db");

// 打开加密文件
using var inputStream = File.OpenRead("encrypted_music.ncm");

// 自动识别解密器
var decrypter = autoDecrypter.GetDecrypter(inputStream);

// 检测音频格式
var audioFormat = decrypter.DetectAudioFormat(inputStream);
Console.WriteLine($"检测到音频格式: {audioFormat}");

// 解密到输出文件
using var outputStream = File.Create($"decrypted_music.{audioFormat.ToString().ToLower()}");
await decrypter.DecryptStreamAsync(inputStream, outputStream, 
    progress: (current, total) => {
        Console.WriteLine($"解密进度: {current}/{total} ({(double)current/total:P})");
    });

Console.WriteLine("解密完成！");
```

### NCM 格式 - 带封面提取

```csharp
using ZStack.MusicDecryptLib.Decrypters;

var ncm = new NCM();

using var inputStream = File.OpenRead("song.ncm");

// 检测音频格式
var format = ncm.DetectAudioFormat(inputStream);

// 解密音频
string outputPath = $"song.{format.ToString().ToLower()}";
using var outputStream = File.Create(outputPath);
await ncm.DecryptStreamAsync(inputStream, outputStream);

// 修补封面图片到解密后的文件
ncm.PatchCoverImage(inputStream, outputPath);

Console.WriteLine($"解密完成，包含封面: {outputPath}");
```

### KGM 格式

```csharp
using ZStack.MusicDecryptLib.Decrypters;

var kgm = new KGM();

using var inputStream = File.OpenRead("song.kgm");
var format = kgm.DetectAudioFormat(inputStream);

using var outputStream = File.Create($"song.{format.ToString().ToLower()}");
await kgm.DecryptStreamAsync(inputStream, outputStream);
```

### KGG 格式

```csharp
using ZStack.MusicDecryptLib.Decrypters;

var kgg = new KGG();

// 加载密钥数据库（必需）
kgg.LoadKeyDic("path/to/kgmusic.db");

using var inputStream = File.OpenRead("song.kgg");
var format = kgg.DetectAudioFormat(inputStream);

using var outputStream = File.Create($"song.{format.ToString().ToLower()}");
await kgg.DecryptStreamAsync(inputStream, outputStream);
```

### 部分解密 - 指定偏移和长度

```csharp
var decrypter = new NCM();
using var inputStream = File.OpenRead("song.ncm");
using var outputStream = File.Create("partial.mp3");

// 从偏移 1024 开始解密 10240 字节
await decrypter.DecryptStreamAsync(
    inputStream, 
    outputStream,
    offset: 1024,
    decryptLength: 10240,
    bufferSize: 4096
);
```

### 使用取消令牌

```csharp
var cts = new CancellationTokenSource();
var decrypter = new KGM();

using var inputStream = File.OpenRead("song.kgm");
using var outputStream = File.Create("song.mp3");

try 
{
    await decrypter.DecryptStreamAsync(
        inputStream, 
        outputStream,
        cancellationToken: cts.Token
    );
}
catch (OperationCanceledException)
{
    Console.WriteLine("解密已取消");
}
```

## 📚 API 文档

### AutoDecrypter

自动识别加密格式并选择对应的解密器。

#### 方法

- `void AddDecrypter(IDecrypter decrypter)` - 添加自定义解密器
- `void AddKGG(string dbFilePath)` - 添加 KGG 解密器并加载密钥数据库
- `IDecrypter GetDecrypter(Stream inputStream)` - 获取匹配的解密器（找不到会抛出异常）
- `bool TryGetDecrypter(Stream inputStream, out IDecrypter? decrypter)` - 尝试获取匹配的解密器

### IDecrypter 接口

所有解密器实现的通用接口。

#### 方法

- `void CheckSupport(Stream inputStream)` - 检查是否支持该文件格式
- `AudioFormat DetectAudioFormat(Stream inputStream)` - 检测音频格式
- `long GetDecryptedSize(Stream inputStream)` - 获取解密后的文件大小
- `Task DecryptStreamAsync(Stream inputStream, Stream outputStream, long offset = 0, long decryptLength = -1, int bufferSize = 81920, Action<long, long>? progress = null, CancellationToken cancellationToken = default)` - 异步解密流

#### 参数说明

- `inputStream` - 加密的输入流（必须支持读取和定位）
- `outputStream` - 解密的输出流（必须支持写入）
- `offset` - 从输入数据的哪个位置开始解密（默认 0）
- `decryptLength` - 解密数据的长度（-1 表示全部）
- `bufferSize` - 内部缓冲区大小（默认 81920 字节）
- `progress` - 进度回调 `Action<long current, long total>`
- `cancellationToken` - 取消令牌

### NCM 类

网易云音乐加密格式解密器。

#### 额外方法

- `void PatchCoverImage(Stream inputStream, string destFilePath)` - 将封面图片修补到解密后的音频文件

### AudioUtils 类

音频格式检测和 MIME 类型工具类。

#### 方法

- `static AudioFormat? GetAudioFormat(byte[] data)` - 根据文件头检测音频格式
- `static string GetMimeType(string fileName)` - 根据文件名获取 MIME 类型

### AudioFormat 枚举

支持的音频格式。

```csharp
public enum AudioFormat
{
    FLAC,   // audio/flac
    MP3,    // audio/mpeg
    OGG,    // audio/ogg
    M4A,    // audio/mp4
    WAV,    // audio/wav
    WMA,    // audio/x-ms-wma
    AAC     // audio/aac
}
```

## 🔍 支持的格式

| 格式 | 扩展名 | 平台 | 是否需要密钥库 |
|------|--------|------|----------------|
| NCM | .ncm | 网易云音乐 | ❌ |
| KGM | .kgm, .kgma | 酷狗音乐 | ❌ |
| VPR | .vpr | 酷狗音乐 | ❌ |
| KGG | .kgg | 酷狗音乐 | ✅ 需要 kgmusic.db |

### 关于 KGG 格式

KGG 格式需要加载酷狗音乐的密钥数据库文件 `kgmusic.db`。该文件通常位于：
- Windows: `%LOCALAPPDATA%\KGMusic\KGMusic\kgmusic.db`
- 其他平台可能位于应用数据目录

## ⚠️ 异常处理

库中定义了自定义异常 `MusicDecryptException`，在以下情况会抛出：

- 文件格式不支持或无效
- 文件头部损坏
- 缺少必要的密钥数据
- 解密过程出错

建议使用 try-catch 捕获：

```csharp
try 
{
    var decrypter = autoDecrypter.GetDecrypter(inputStream);
    await decrypter.DecryptStreamAsync(inputStream, outputStream);
}
catch (MusicDecryptException ex)
{
    Console.WriteLine($"解密失败: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"未知错误: {ex.Message}");
}
```

## 🛠️ 高级用法

### 自定义解密器

可以实现 `IDecrypter` 接口来添加对新格式的支持：

```csharp
public class MyCustomDecrypter : IDecrypter
{
    public void CheckSupport(Stream inputStream)
    {
        // 检查文件格式
        // 不支持则抛出 MusicDecryptException
    }

    public AudioFormat DetectAudioFormat(Stream inputStream)
    {
        // 实现格式检测
    }

    public long GetDecryptedSize(Stream inputStream)
    {
        // 返回解密后大小
    }

    public async Task DecryptStreamAsync(
        Stream inputStream, Stream outputStream,
        long offset = 0, long decryptLength = -1,
        int bufferSize = 81920, 
        Action<long, long>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        // 实现解密逻辑
    }
}

// 使用自定义解密器
var autoDecrypter = new AutoDecrypter();
autoDecrypter.AddDecrypter(new MyCustomDecrypter());
```

## 📋 系统要求

- .NET Standard 2.1 或更高版本
- 支持的平台：
  - .NET Core 3.0+
  - .NET 5.0+

## 📦 依赖项

- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/) (>= 10.0.0) - SQLite 数据库支持
- [TagLibSharp](https://www.nuget.org/packages/TagLibSharp/) (>= 2.3.0) - 音频元数据和封面处理

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用 [MIT 许可证](https://opensource.org/licenses/MIT)。

## 🔗 相关链接

- [GitHub 仓库](https://github.com/zuozishi/ZStack.MusicDecryptLib)
- [NuGet 包](https://www.nuget.org/packages/ZStack.MusicDecryptLib)

## ⚖️ 免责声明

本库仅供学习和研究使用。请尊重版权，不要用于非法用途。解密后的音频文件仅供个人学习使用，请勿传播。

---

如有问题或建议，欢迎在 GitHub 上提交 Issue。