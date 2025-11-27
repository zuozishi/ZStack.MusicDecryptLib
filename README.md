# ZStack.MusicDecryptLib

<div align="center">

一个功能完善的 .NET 音乐解密项目，支持多种音乐平台的加密格式解密。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Standard 2.1](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

</div>

---

## 🎯 项目简介

**ZStack.MusicDecryptLib** 是一个用于解密各种音乐平台加密音乐文件的完整解决方案，包含以下两个主要组件：

### 📦 核心库 (ZStack.MusicDecryptLib)

一个高性能、易扩展的 .NET Standard 2.1 类库，提供音乐文件解密的核心功能：
- ✅ 支持多种加密格式（NCM、KGM、KGMA、KGG）
- ✅ 异步流式处理，内存占用低
- ✅ 自动格式识别和解密器选择
- ✅ 可扩展的解密器架构
- ✅ 封面图片提取和嵌入（NCM 格式）

### 🖥️ 控制台工具 (MusicDecrypt.exe)

一个功能强大的命令行工具，基于核心库构建：
- ✅ 单文件和批量处理
- ✅ 递归目录扫描
- ✅ 进度显示和详细日志
- ✅ 支持 Native AOT 编译，快速启动

---

## 📁 项目结构

```
ZStack.MusicDecryptLib/
│
├── README.md                          # 本文件 - 解决方案总体说明
├── ZStack.MusicDecryptLib.slnx        # Visual Studio 解决方案文件
│
├── ZStack.MusicDecryptLib/            # 核心解密库项目
│   ├── README.md                      # 库的详细使用文档和 API 说明
│   ├── ZStack.MusicDecryptLib.csproj  # 项目文件（.NET Standard 2.1）
│   ├── AutoDecrypter.cs               # 自动解密器（格式识别）
│   ├── AudioFormat.cs                 # 音频格式枚举
│   ├── AudioUtils.cs                  # 音频格式检测工具
│   ├── MusicDecryptException.cs       # 自定义异常
│   ├── Decrypters/                    # 解密器实现
│   │   ├── IDecrypter.cs             # 解密器接口
│   │   ├── NCM.cs                    # 网易云音乐解密器
│   │   ├── KGM.cs                    # 酷狗音乐 KGM 解密器
│   │   └── KGG.cs                    # 酷狗音乐 KGG 解密器
│   ├── Extensions/                    # 扩展方法
│   │   └── StreamExtensions.cs       # Stream 扩展
│   └── Internal/                      # 内部实现
│       ├── KGDatabase.cs             # 酷狗数据库处理
│       └── QMC2.cs                   # QMC2 加密算法
│
└── ConsoleApp/                        # 控制台应用项目
    ├── README.md                      # 控制台工具使用文档
    ├── MusicDecryptConsoleApp.csproj  # 项目文件（.NET 10.0）
    ├── Program.cs                     # 主程序入口
    └── Properties/
        └── launchSettings.json        # 启动配置
```

### 项目说明

| 项目 | 类型 | 目标框架 | 用途 |
|------|------|---------|------|
| **ZStack.MusicDecryptLib** | 类库 | .NET Standard 2.1 | 核心解密功能，可被其他 .NET 项目引用 |
| **ConsoleApp** | 可执行程序 | .NET 10.0 | 命令行工具，提供批量解密功能 |

---

## 🎵 支持的格式

本解决方案支持以下音乐平台的加密格式：

| 格式 | 扩展名 | 来源平台 | 是否需要密钥库 | 解密器类 |
|------|--------|---------|---------------|---------|
| **NCM** | `.ncm` | 网易云音乐 | ❌ 否 | `NCM` |
| **KGM** | `.kgm`, `.kgma` | 酷狗音乐 | ❌ 否 | `KGM` |
| **KGG** | `.kgg` | 酷狗音乐 | ✅ 是 | `KGG` |

### 输出格式

解密后会自动识别原始音频格式，支持：
- **FLAC** - 无损音频格式
- **MP3** - 有损压缩格式
- **AAC/M4A** - 高效音频编码
- **OGG** - Vorbis 编码
- **WAV** - 未压缩格式
- **WMA** - Windows Media Audio

---

## 🚀 快速开始

### 方式一：使用控制台工具（推荐新手）

1. **下载/构建程序**
   ```powershell
   # 克隆仓库
   git clone https://github.com/zuozishi/ZStack.MusicDecryptLib.git
   cd ZStack.MusicDecryptLib/ConsoleApp
   
   # 构建发布版本
   dotnet publish -c Release
   ```

2. **运行解密**
   ```powershell
   # 解密单个文件
   .\MusicDecrypt.exe -i "music.ncm" -o "output"
   
   # 批量解密目录
   .\MusicDecrypt.exe -i "encrypted_folder" -o "decrypted_folder" -r
   ```

### 方式二：作为类库使用（开发者）

1. **安装 NuGet 包**
   ```powershell
   dotnet add package ZStack.MusicDecryptLib
   ```

2. **编写代码**
   ```csharp
   using ZStack.MusicDecryptLib;
   
   // 创建自动解密器
   var autoDecrypter = new AutoDecrypter();
   
   // 打开加密文件
   using var input = File.OpenRead("song.ncm");
   
   // 获取解密器并检测格式
   var decrypter = autoDecrypter.GetDecrypter(input);
   var format = decrypter.DetectAudioFormat(input);
   
   // 解密到输出文件
   using var output = File.Create($"song.{format.ToString().ToLower()}");
   await decrypter.DecryptStreamAsync(input, output);
   ```

---

## 💡 核心功能

### 1. 自动格式识别

使用 `AutoDecrypter` 类自动识别加密格式，无需手动指定：

```csharp
var autoDecrypter = new AutoDecrypter();
using var stream = File.OpenRead("unknown_file.xxx");

// 自动识别并获取对应解密器
if (autoDecrypter.TryGetDecrypter(stream, out var decrypter))
{
    // 解密...
}
```

### 2. 流式处理

支持流式解密，适合处理大文件，内存占用低：

```csharp
await decrypter.DecryptStreamAsync(
    inputStream, 
    outputStream,
    bufferSize: 81920,  // 80KB 缓冲区
    progress: (current, total) => {
        Console.WriteLine($"进度: {current}/{total}");
    }
);
```

### 3. 部分解密

支持指定偏移量和长度进行部分解密：

```csharp
// 从 1024 字节开始解密 10240 字节
await decrypter.DecryptStreamAsync(
    inputStream, 
    outputStream,
    offset: 1024,
    decryptLength: 10240
);
```

### 4. 封面提取（NCM 专属）

自动提取网易云音乐的封面并嵌入到解密文件：

```csharp
var ncm = new NCM();
// 解密...
ncm.PatchCoverImage(inputStream, outputFilePath);
```

### 5. 取消支持

所有异步操作支持取消令牌：

```csharp
var cts = new CancellationTokenSource();
await decrypter.DecryptStreamAsync(
    inputStream, 
    outputStream,
    cancellationToken: cts.Token
);
```

---

## 🏗️ 技术架构

### 设计模式

#### 1. 策略模式 (Strategy Pattern)
- **`IDecrypter`** 接口定义解密器的通用行为
- 每种加密格式实现一个具体策略（`NCM`, `KGM`, `KGG`）
- `AutoDecrypter` 作为上下文选择合适的策略

```
┌─────────────────┐
│  IDecrypter     │  ◄─── 接口
├─────────────────┤
│ CheckSupport()  │
│ DetectFormat()  │
│ DecryptStream() │
└─────────────────┘
        △
        │ implements
        ├───────────┬──────────┬──────────
        │           │          │
    ┌───┴───┐  ┌───┴───┐  ┌───┴───┐
    │  NCM  │  │  KGM  │  │  KGG  │
    └───────┘  └───────┘  └───────┘
```

#### 2. 工厂模式 (Factory Pattern)
- `AutoDecrypter` 根据文件头自动创建合适的解密器实例

#### 3. 模板方法模式 (Template Method)
- `IDecrypter` 定义解密流程框架
- 子类实现具体的解密算法细节

### 核心类图

```
AutoDecrypter
    │
    ├── Dictionary<Type, IDecrypter>
    │
    ├── GetDecrypter(Stream)
    └── TryGetDecrypter(Stream)
            │
            │ 返回
            ▼
        IDecrypter
            │
            ├── NCM (网易云)
            │   ├── AES-128 解密
            │   └── 封面提取
            │
            ├── KGM (酷狗)
            │   ├── VPR 模式
            │   ├── KGM 模式
            │   └── 混合加密
            │
            └── KGG (酷狗新版)
                ├── TEA 解密
                ├── RC4 变种
                └── SQLite 密钥库
```

### 关键算法

| 加密格式 | 加密算法 | 实现位置 |
|---------|---------|---------|
| NCM | AES-128-ECB | `NCM.cs` |
| KGM | XOR + 字节表 | `KGM.cs` |
| KGG | TEA + RC4 变种 | `QMC2.cs`, `KGG.cs` |

---

## 📖 使用文档

### 详细文档

- **核心库文档**: [ZStack.MusicDecryptLib/README.md](./ZStack.MusicDecryptLib/README.md)
  - API 详细说明
  - 完整代码示例
  - 异常处理指南
  - 高级用法和自定义解密器

- **控制台工具文档**: [ConsoleApp/README.md](./ConsoleApp/README.md)
  - 命令行参数说明
  - 使用场景示例
  - 故障排除指南
  - 性能优化建议

### 常见使用场景

#### 场景 1: 批量转换本地音乐文件

```powershell
MusicDecrypt -i "C:\MyMusic" -o "C:\MyDecryptedMusic" -r -w
```

#### 场景 2: 在自己的项目中集成

```csharp
// 作为服务使用
public class MusicDecryptService
{
    private readonly AutoDecrypter _decrypter = new();
    
    public async Task<string> DecryptAsync(string inputPath, string outputDir)
    {
        using var input = File.OpenRead(inputPath);
        var decrypter = _decrypter.GetDecrypter(input);
        var format = decrypter.DetectAudioFormat(input);
        
        var outputPath = Path.Combine(outputDir, 
            Path.ChangeExtension(Path.GetFileName(inputPath), 
            format.ToString().ToLower()));
        
        using var output = File.Create(outputPath);
        await decrypter.DecryptStreamAsync(input, output);
        
        return outputPath;
    }
}
```

#### 场景 3: Web API 集成

```csharp
[HttpPost("decrypt")]
public async Task<IActionResult> Decrypt(IFormFile file)
{
    var autoDecrypter = new AutoDecrypter();
    
    using var inputStream = file.OpenReadStream();
    var decrypter = autoDecrypter.GetDecrypter(inputStream);
    var format = decrypter.DetectAudioFormat(inputStream);
    
    using var outputStream = new MemoryStream();
    await decrypter.DecryptStreamAsync(inputStream, outputStream);
    
    return File(outputStream.ToArray(), 
        $"audio/{format.ToString().ToLower()}", 
        $"decrypted.{format.ToString().ToLower()}");
}
```

---

## 🔨 构建和发布

### 构建核心库

```powershell
cd ZStack.MusicDecryptLib
dotnet build -c Release
```

### 打包 NuGet

```powershell
dotnet pack -c Release -o ./nupkg
```

### 构建控制台工具

```powershell
cd ConsoleApp

# 标准构建
dotnet build -c Release

# 发布单文件（包含运行时）
dotnet publish -c Release -r win-x64 --self-contained

# AOT 编译（更快启动，更小体积）
dotnet publish -c Release -r win-x64 /p:PublishAot=true
```

### 发布选项对比

| 发布方式 | 体积 | 启动速度 | 依赖 .NET 运行时 |
|---------|------|---------|----------------|
| 标准发布 | ~200KB | 慢 | ✅ 需要 |
| Self-Contained | ~70MB | 中等 | ❌ 不需要 |
| AOT 编译 | ~10MB | 极快 | ❌ 不需要 |

---

## 💻 系统要求

### 核心库 (ZStack.MusicDecryptLib)

- **.NET Standard 2.1** 或更高版本
- 兼容平台：
  - .NET Core 3.0+
  - .NET 5.0+
  - .NET 6.0+
  - .NET 7.0+
  - .NET 8.0+
  - .NET 9.0+
  - .NET 10.0+

### 控制台工具 (MusicDecrypt)

- **.NET 10.0 运行时**（如果使用标准版本）
- 或使用 Self-Contained / AOT 版本无需安装运行时

### 依赖包

| 包名 | 版本 | 用途 |
|------|------|------|
| Microsoft.Data.Sqlite | ≥10.0.0 | KGG 格式密钥数据库 |
| TagLibSharp | ≥2.3.0 | 音频元数据和封面处理 |
| CommandLineParser | ≥2.9.1 | 命令行参数解析（仅控制台） |
| Serilog.Sinks.Console | ≥6.1.1 | 日志输出（仅控制台） |

---

## 📄 许可证

本项目采用 **MIT 许可证** - 详见 [LICENSE](LICENSE) 文件。

---

## ⚖️ 免责声明

**重要提示：请务必阅读并遵守**

1. **仅供学习研究**: 本项目及其所有组件仅供学习和技术研究使用。

2. **尊重版权**: 解密后的音频文件受原版权方保护，请勿用于任何商业用途或非法传播。

3. **个人使用**: 解密后的文件仅限个人学习和备份使用，不得分享、上传或公开传播。

4. **合法性**: 用户需自行承担使用本工具的法律责任。请确保你对加密文件拥有合法使用权。

5. **支持正版**: 鼓励用户购买正版音乐服务，支持音乐创作者。

6. **无担保**: 本软件按"原样"提供，不提供任何明示或暗示的保证。

**使用本工具即表示你已阅读、理解并同意遵守以上条款。**

---

## 🔗 相关链接

- **GitHub 仓库**: [https://github.com/zuozishi/ZStack.MusicDecryptLib](https://github.com/zuozishi/ZStack.MusicDecryptLib)
- **NuGet 包**: [https://www.nuget.org/packages/ZStack.MusicDecryptLib](https://www.nuget.org/packages/ZStack.MusicDecryptLib)
- **问题反馈**: [GitHub Issues](https://github.com/zuozishi/ZStack.MusicDecryptLib/issues)
- **讨论区**: [GitHub Discussions](https://github.com/zuozishi/ZStack.MusicDecryptLib/discussions)

---

## 🙏 致谢

感谢所有贡献者和以下开源项目：

- [ncmdump](https://github.com/taurusxin/ncmdump)
- [AudioDecrypt](https://github.com/0x77fe/AudioDecrypt)

---

<div align="center">

**⭐ 如果这个项目对你有帮助，请给个 Star！⭐**

Made with ❤️ by [Zuozishi](https://github.com/zuozishi)

</div>
