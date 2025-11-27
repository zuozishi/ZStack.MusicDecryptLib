using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TagLib;
using ZStack.MusicDecryptLib.Extensions;

namespace ZStack.MusicDecryptLib.Decrypters;

public class NCM : IDecrypter
{
    /// <inheritdoc />
    public void CheckSupport(Stream inputStream)
    {
        if (!inputStream.CanRead)
            throw new ArgumentException("输入流不可读", nameof(inputStream));
        if (!inputStream.CanSeek)
            throw new ArgumentException("输入流不支持定位", nameof(inputStream));
        if (inputStream.Length < MAGIC_HEADER.Length)
            throw new MusicDecryptException("不是有效的NCM文件格式");

        inputStream.Seek(0, SeekOrigin.Begin);

        byte[] header = new byte[MAGIC_HEADER.Length];
        inputStream.Read(header);

        if (!MAGIC_HEADER.SequenceEqual(header))
            throw new MusicDecryptException("不是有效的NCM文件格式");
    }

    /// <inheritdoc />
    public AudioFormat DetectAudioFormat(Stream inputStream)
    {
        inputStream.Seek(10, SeekOrigin.Begin);
        _ = inputStream.ReadChunk();
        byte[] dontModifyChunk = inputStream.ReadChunk();
        int startIndex = 0;
        for (int i = 0; i < dontModifyChunk.Length; i++)
        {
            dontModifyChunk[i] ^= 0x63;
            if (dontModifyChunk[i] == 58 && startIndex == 0)
                startIndex = i + 1;
        }
        string base64 = Encoding.UTF8.GetString(dontModifyChunk, startIndex, dontModifyChunk.Length - startIndex);
        byte[] dontModifyDecryptChunk = Convert.FromBase64String(base64);
        dontModifyDecryptChunk = AesDecrypt(dontModifyDecryptChunk, MODIFY_KEY)[6..];
        var metadata = Encoding.UTF8.GetString(dontModifyDecryptChunk);
        Regex regex = new(@"""format"":""(\w+)""");
        var match = regex.Match(metadata);
        if (!match.Success)
            throw new MusicDecryptException("元数据信息中不包含媒体格式");
        string format = match.Groups[1].Value;
        return format.ToLower() switch
        {
            "flac" => AudioFormat.FLAC,
            "mp3" => AudioFormat.MP3,
            "ogg" => AudioFormat.OGG,
            "m4a" => AudioFormat.M4A,
            "wav" => AudioFormat.WAV,
            "wma" => AudioFormat.WMA,
            "aac" => AudioFormat.AAC,
            _ => throw new MusicDecryptException($"不支持的音频格式：{format}"),
        };
    }

    /// <inheritdoc />
    public async Task DecryptStreamAsync(
        Stream inputStream, Stream outputStream,
        long offset = 0, long decryptLength = -1, int bufferSize = 81920,
        Action<long, long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!outputStream.CanWrite)
            throw new ArgumentException("输出流不可写", nameof(outputStream));
        if (offset < 0)
            throw new ArgumentException("偏移量不能为负数", nameof(offset));

        CheckSupport(inputStream);
        inputStream.Seek(10, SeekOrigin.Begin);

        byte[] coreKeyChunk = inputStream.ReadChunk();
        for (int i = 0; i < coreKeyChunk.Length; i++)
        {
            coreKeyChunk[i] ^= 0x64;
        }
        byte[] finalKey = AesDecrypt(coreKeyChunk, CORE_KEY)[17..];

        var keyBox = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            keyBox[i] = (byte)i;
        }

        byte swap, c, last_byte = 0, key_offset = 0;
        for (int i = 0; i < keyBox.Length; i++)
        {
            swap = keyBox[i];
            c = (byte)((swap + last_byte + finalKey[key_offset++]) & 0xff);
            if (key_offset >= finalKey.Length) key_offset = 0;
            keyBox[i] = keyBox[c];
            keyBox[c] = swap;
            last_byte = c;
        }

        _ = inputStream.ReadChunk(); // dontModifyChunk
        inputStream.Seek(9, SeekOrigin.Current); // skip crc
        _ = inputStream.ReadChunk(); // imageChunk

        long dataLength = inputStream.Length - inputStream.Position;
        if (decryptLength < 0)
            decryptLength = dataLength - offset;

        if (offset >= dataLength)
            throw new ArgumentException("偏移量超出数据范围", nameof(offset));
        if (offset + decryptLength > dataLength)
            throw new ArgumentException("解密长度超出数据范围", nameof(decryptLength));

        inputStream.Seek(offset, SeekOrigin.Current);

        byte[] buffer = new byte[bufferSize];
        long currentOffset = offset;
        long remainingBytes = decryptLength;
        int bytesRead;

        while (remainingBytes > 0 && (bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bufferSize, remainingBytes)), cancellationToken)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                int j = (i + 1) & 0xff;
                buffer[i] ^= keyBox[(keyBox[j] + keyBox[(keyBox[j] + j) & 0xff]) & 0xff];
            }
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            currentOffset += bytesRead;
            remainingBytes -= bytesRead;
            progress?.Invoke(currentOffset - offset, decryptLength);
        }
    }

    /// <inheritdoc />
    public long GetDecryptedSize(Stream inputStream)
    {
        inputStream.Seek(10, SeekOrigin.Begin);
        _ = inputStream.ReadChunk(); // coreKeyChunk
        _ = inputStream.ReadChunk(); // dontModifyChunk
        inputStream.Seek(9, SeekOrigin.Current); // skip crc
        _ = inputStream.ReadChunk(); // imageChunk
        return inputStream.Length - inputStream.Position;
    }

    /// <summary>
    /// 修补封面图片到目标文件
    /// </summary>
    /// <param name="inputStream">原始输入流</param>
    /// <param name="destFilePath">目标文件</param>
    public void PatchCoverImage(Stream inputStream, string destFilePath)
    {
        inputStream.Seek(10, SeekOrigin.Begin);
        _ = inputStream.ReadChunk(); // coreKeyChunk
        _ = inputStream.ReadChunk(); // dontModifyChunk
        inputStream.Seek(9, SeekOrigin.Current); // skip crc
        byte[] imageChunk = inputStream.ReadChunk();
        if (imageChunk.Length <= 10)
            return;
        using var tfile = TagLib.File.Create(destFilePath);
        tfile.Tag.Pictures =
        [
            new Picture
            {
                Type = PictureType.FrontCover,
                MimeType = "image/jpeg",
                Data = imageChunk
            }
        ];
        tfile.Save();
    }

    private static byte[] AesDecrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Key = key;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }

    static readonly byte[] MAGIC_HEADER = [0x43, 0x54, 0x45, 0x4E, 0x46, 0x44, 0x41, 0x4D];
    static readonly byte[] CORE_KEY = [0x68, 0x7A, 0x48, 0x52, 0x41, 0x6D, 0x73, 0x6F, 0x35, 0x6B, 0x49, 0x6E, 0x62, 0x61, 0x78, 0x57];
    static readonly byte[] MODIFY_KEY = [0x23, 0x31, 0x34, 0x6C, 0x6A, 0x6B, 0x5F, 0x21, 0x5C, 0x5D, 0x26, 0x30, 0x55, 0x3C, 0x27, 0x28];
}
