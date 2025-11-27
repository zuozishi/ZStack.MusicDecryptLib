# MusicDecrypt - 音乐解密控制台程序

一个基于 [ZStack.MusicDecryptLib](../ZStack.MusicDecryptLib) 的命令行工具，用于批量解密加密的音乐文件。

## ✨ 特性

- 🎵 支持多种音乐加密格式（NCM、KGM、KGMA、KGG）
- 📁 支持单文件和批量目录处理
- 🔄 支持递归处理子目录

## 📦 安装

### 从源码构建

```powershell
# 克隆仓库
git clone https://github.com/zuozishi/ZStack.MusicDecryptLib.git
cd ZStack.MusicDecryptLib/ConsoleApp

# 构建项目
dotnet build -c Release

# 发布单文件可执行程序（支持 AOT 编译）
dotnet publish -c Release
```

发布后的可执行文件位于 `bin/Release/net10.0/publish/` 目录。

## 🚀 快速开始

### 基本用法

解密单个文件：

```powershell
MusicDecrypt -i "encrypted_song.ncm" -o "output_folder"
```

批量解密目录中的所有加密文件：

```powershell
MusicDecrypt -i "encrypted_music_folder" -o "output_folder"
```

递归处理目录及其子目录：

```powershell
MusicDecrypt -i "music_folder" -o "output_folder" -r
```

覆盖已存在的文件：

```powershell
MusicDecrypt -i "encrypted_music" -o "output" -w
```

### 解密 KGG 格式

KGG 格式需要酷狗音乐的密钥数据库文件。程序会自动尝试从默认位置加载：

```
%USERPROFILE%\AppData\Roaming\KuGou8\KGMusicV3.db
```

如果数据库在其他位置，可以手动指定：

```powershell
MusicDecrypt -i "song.kgg" -o "output" --kgdb "C:\path\to\KGMusicV3.db"
```

## 📋 命令行参数

| 参数 | 缩写 | 必需 | 说明 | 默认值 |
|------|------|------|------|--------|
| `--input` | `-i` | ✅ | 待解密的文件或目录路径 | - |
| `--output` | `-o` | ✅ | 解密后文件的输出目录 | - |
| `--overwrite` | `-w` | ❌ | 是否覆盖已存在的文件 | `false` |
| `--recursive` | `-r` | ❌ | 是否递归处理子目录（仅目录模式有效） | `false` |
| `--kgdb` | - | ❌ | KGG 格式密钥数据库文件路径 | 自动检测 |

### 参数说明

- **input**: 可以是单个加密文件的路径，或包含加密文件的目录路径
- **output**: 解密后的文件将保存到此目录，如果目录不存在会自动创建
- **overwrite**: 默认情况下，如果输出文件已存在会跳过。使用此选项将覆盖已有文件
- **recursive**: 在目录模式下，启用此选项会递归搜索所有子目录中的加密文件
- **kgdb**: 仅在需要解密 KGG 格式文件时使用。如果未指定且找不到默认位置的数据库，KGG 文件将无法解密

## 📖 使用示例

### 示例 1: 解密单个 NCM 文件

```powershell
MusicDecrypt -i "C:\Music\song.ncm" -o "C:\Output"
```

输出示例：
```
[INF] 输入[文件]: C:\Music\song.ncm
[INF] 输出目录: C:\Output
[INF] 找到 1 个待解密文件
[INF] [1/1] 处理文件: song.ncm
[INF] 成功解密: song.mp3
[INF] 处理完成！
```

### 示例 2: 批量解密目录

```powershell
MusicDecrypt -i "C:\EncryptedMusic" -o "C:\DecryptedMusic"
```

输出示例：
```
[INF] 输入[目录]: C:\EncryptedMusic
[INF] 输出目录: C:\DecryptedMusic
[INF] 找到 15 个待解密文件
[INF] [1/15] 处理文件: song1.ncm
[INF] 成功解密: song1.mp3
[INF] [2/15] 处理文件: song2.kgm
[INF] 成功解密: song2.flac
...
[INF] 处理完成！
```

### 示例 3: 递归处理带覆盖

```powershell
MusicDecrypt -i "D:\Music" -o "D:\Decrypted" -r -w
```

此命令会：
- 递归搜索 `D:\Music` 及其所有子目录
- 解密所有支持的加密文件
- 如果输出文件已存在则覆盖

### 示例 4: 使用相对路径

```powershell
MusicDecrypt -i ".\encrypted" -o ".\output"
```

程序会自动将相对路径转换为绝对路径。

### 示例 5: 指定 KGG 数据库

```powershell
MusicDecrypt -i "music.kgg" -o "output" --kgdb "D:\KuGou\KGMusicV3.db"
```

## 🎯 支持的格式

程序会自动识别以下格式的加密文件：

| 格式 | 扩展名 | 平台 | 输出格式 |
|------|--------|------|----------|
| NCM | `.ncm` | 网易云音乐 | MP3、FLAC 等 |
| KGM | `.kgm`、`.kgma` | 酷狗音乐 | MP3、FLAC 等 |
| KGG | `.kgg` | 酷狗音乐 | MP3、FLAC 等 |

输出格式会根据原始音频编码自动确定（如 MP3、FLAC、M4A 等）。

## 🔍 工作原理

1. **文件扫描**: 根据输入参数扫描待处理的文件
2. **格式识别**: 使用 `AutoDecrypter` 自动识别加密格式
3. **音频检测**: 检测原始音频格式（MP3、FLAC 等）
4. **解密处理**: 异步解密文件并保存到输出目录
5. **封面提取**: 对于 NCM 格式，自动提取并嵌入封面图片

## ⚙️ 技术细节

### 依赖项

- **ZStack.MusicDecryptLib**: 核心解密库
- **CommandLineParser**: 命令行参数解析
- **Serilog.Sinks.Console**: 日志输出

### 项目配置

- **目标框架**: .NET 10.0
- **输出类型**: 可执行文件 (Exe)
- **程序集名称**: `MusicDecrypt.exe`
- **AOT 编译**: 支持（`PublishAot=True`）
- **裁剪**: 支持（`PublishTrimmed=True`）

### 性能优化

- 使用异步 I/O 提高处理速度
- 流式处理避免大文件内存占用
- 支持 Native AOT 编译，启动更快，内存占用更小

## 🛠️ 故障排除

### 问题 1: "未找到 kgdb 文件，无法解密 kgg 格式文件"

**原因**: KGG 格式需要密钥数据库，但程序未找到。

**解决方案**:
1. 确保已安装酷狗音乐客户端
2. 使用 `--kgdb` 参数手动指定数据库路径
3. 检查数据库文件是否存在于默认位置

### 问题 2: "不支持的文件格式，跳过"

**原因**: 文件可能已经是解密后的格式，或者不是支持的加密格式。

**解决方案**:
- 确认文件确实是加密格式（.ncm、.kgm、.kgma、.kgg）
- 检查文件是否损坏

### 问题 3: "无权限访问文件"

**原因**: 文件或目录权限不足。

**解决方案**:
- 以管理员身份运行程序
- 检查文件和目录的访问权限
- 确保输出目录有写入权限

### 问题 4: "输出文件已存在且未指定覆盖，跳过"

**原因**: 输出文件已存在，且未使用 `--overwrite` 参数。

**解决方案**:
- 添加 `-w` 或 `--overwrite` 参数以覆盖已有文件
- 或手动删除/移动已存在的输出文件

## 📊 日志级别

程序使用 Serilog 进行日志输出，包含以下级别：

- **[INF]** (Information): 一般信息，如处理进度
- **[WRN]** (Warning): 警告信息，如跳过的文件
- **[ERR]** (Error): 错误信息，如处理失败的文件

## 🔒 安全性和合法性

**重要提示**:
- 本工具仅供学习和研究使用
- 解密后的音频文件仅供个人学习和备份使用
- 请勿传播或用于商业用途
- 请尊重音乐版权，支持正版音乐

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

如果您发现 Bug 或有功能建议，请在 GitHub 上创建 Issue。

## 📄 许可证

本项目采用 [MIT 许可证](https://opensource.org/licenses/MIT)。

## 🔗 相关链接

- [ZStack.MusicDecryptLib 核心库](../ZStack.MusicDecryptLib)
- [GitHub 仓库](https://github.com/zuozishi/ZStack.MusicDecryptLib)

## 💡 提示

- 建议先在少量文件上测试，确认输出正常后再批量处理
- 使用 `-r` 参数时注意，可能会处理大量文件，确保有足够的磁盘空间
- 解密后的文件保持原始音质，不会进行二次编码
- NCM 格式的封面信息会自动提取并嵌入到输出文件中

---

如有问题或需要帮助，请访问 [GitHub Issues](https://github.com/zuozishi/ZStack.MusicDecryptLib/issues)。
