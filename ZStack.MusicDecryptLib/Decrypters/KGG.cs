using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZStack.MusicDecryptLib.Extensions;
using ZStack.MusicDecryptLib.Internal;

namespace ZStack.MusicDecryptLib.Decrypters;

/// <summary>
/// KGG格式解密器
/// </summary>
public class KGG : IDecrypter
{
    public Dictionary<string, string> KeyDic { get; private set; } = [];

    /// <summary>
    /// 加载密钥数据库
    /// </summary>
    /// <param name="dbFilePath"></param>
    public void LoadKeyDic(string dbFilePath)
    {
        var db = new KGDatabase(dbFilePath);
        KeyDic = db.ReadKeyMap();
    }

    /// <inheritdoc />
    public void CheckSupport(Stream inputStream)
    {
        if (!inputStream.CanRead)
            throw new ArgumentException("输入流不可读", nameof(inputStream));
        if (!inputStream.CanSeek)
            throw new ArgumentException("输入流不支持定位", nameof(inputStream));

        inputStream.Seek(20, SeekOrigin.Begin);
        uint mode = inputStream.ReadUInt32();
        if (mode != 5)
            throw new MusicDecryptException("不是有效的KGG文件格式");
    }

    /// <inheritdoc />
    public AudioFormat DetectAudioFormat(Stream inputStream)
    {
        var bytes = new byte[8];
        using var ms = new MemoryStream(bytes);
        DecryptStreamAsync(inputStream, ms, decryptLength: bytes.Length).GetAwaiter().GetResult();
        return AudioUtils.GetAudioFormat(bytes)
            ?? throw new MusicDecryptException("不支持的音频格式");
    }

    /// <inheritdoc />
    public long GetDecryptedSize(Stream inputStream)
    {
        if (!inputStream.CanRead)
            throw new ArgumentException("输入流不可读", nameof(inputStream));
        if (!inputStream.CanSeek)
            throw new ArgumentException("输入流不支持定位", nameof(inputStream));

        var headerLen = ReadHeaderLen(inputStream);
        return inputStream.Length - headerLen;
    }

    /// <inheritdoc />
    public async Task DecryptStreamAsync(
        Stream inputStream, Stream outputStream,
        long offset = 0, long decryptLength = -1,
        int bufferSize = 81920, Action<long, long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!inputStream.CanRead)
            throw new ArgumentException("输入流不可读", nameof(inputStream));
        if (!inputStream.CanSeek)
            throw new ArgumentException("输入流不支持定位", nameof(inputStream));
        if (!outputStream.CanWrite)
            throw new ArgumentException("输出流不可写", nameof(outputStream));
        if (offset < 0)
            throw new ArgumentException("偏移量不能为负数", nameof(offset));

        CheckSupport(inputStream);

        var headerLen = ReadHeaderLen(inputStream);
        var key = FindKey(inputStream);
        if (key == null || !KeyDic.TryGetValue(key, out string? secret))
            throw new MusicDecryptException($"无法找到解密密钥: {key} (解密前请先调用LoadKeyDic)");
        var decryptor = CreateDecryptor(secret)
            ?? throw new MusicDecryptException("解密器创建失败");

        long dataStartPosition = headerLen;
        long dataLength = inputStream.Length - headerLen;

        if (decryptLength < 0)
            decryptLength = dataLength - offset;

        if (offset >= dataLength)
            throw new ArgumentException("偏移量超出数据范围", nameof(offset));
        if (offset + decryptLength > dataLength)
            throw new ArgumentException("解密长度超出数据范围", nameof(decryptLength));

        inputStream.Seek(dataStartPosition + offset, SeekOrigin.Begin);

        long currentOffset = offset;
        long remainingBytes = decryptLength;
        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        while (remainingBytes > 0 && (bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bufferSize, remainingBytes)), cancellationToken)) > 0)
        {
            Span<byte> dataSpan = buffer.AsSpan(0, bytesRead);
            decryptor.Decrypt(dataSpan, currentOffset);
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            currentOffset += bytesRead;
            remainingBytes -= bytesRead;
            progress?.Invoke(currentOffset - offset, decryptLength);
        }
    }

    private uint ReadHeaderLen(Stream inputStream)
    {
        inputStream.Seek(16, SeekOrigin.Begin);
        return inputStream.ReadUInt32();
    }

    private string FindKey(Stream inputStream)
    {
        inputStream.Seek(68, SeekOrigin.Begin);
        byte[] keyBytes = inputStream.ReadChunk();
        return Encoding.UTF8.GetString(keyBytes);
    }

    private Qmc2Base? CreateDecryptor(string secret)
    {
        return Qmc2Factory.Create(secret);
    }
}
