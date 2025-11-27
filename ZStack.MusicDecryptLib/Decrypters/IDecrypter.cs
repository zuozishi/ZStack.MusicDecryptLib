using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZStack.MusicDecryptLib.Decrypters;

/// <summary>
/// 音乐解密器接口
/// </summary>
public interface IDecrypter
{
    /// <summary>
    /// 检查输入流是否为支持的加密格式
    /// </summary>
    /// <param name="inputStream">输入流</param>
    void CheckSupport(Stream inputStream);

    /// <summary>
    /// 检测音频格式
    /// </summary>
    /// <param name="inputStream">输入流</param>
    /// <returns></returns>
    AudioFormat DetectAudioFormat(Stream inputStream);

    /// <summary>
    /// 获取解密后的文件大小
    /// </summary>
    /// <param name="inputStream">输入流</param>
    /// <returns></returns>
    long GetDecryptedSize(Stream inputStream);

    /// <summary>
    /// 异步解密流
    /// </summary>
    /// <param name="inputStream">输入流</param>
    /// <param name="outputStream">输出流</param>
    /// <param name="offset">偏移量</param>
    /// <param name="decryptLength">解密量</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <param name="progress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task DecryptStreamAsync(
        Stream inputStream, Stream outputStream,
        long offset = 0, long decryptLength = -1,
        int bufferSize = 81920, Action<long, long>? progress = null, CancellationToken cancellationToken = default);
}
