using System.Collections.Generic;
using System.IO;

namespace ZStack.MusicDecryptLib;

/// <summary>
/// 用于音频格式检测和MIME类型检索的实用程序类
/// </summary>
public static class AudioUtils
{
    /// <summary>
    /// 根据音频文件的字节数据检测音频格式
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static AudioFormat? GetAudioFormat(byte[] data)
    {
        foreach (var kvp in HEADERS)
        {
            var format = kvp.Key;
            var header = kvp.Value;
            if (data.Length >= header.Length)
            {
                bool match = true;
                for (int i = 0; i < header.Length; i++)
                {
                    if (data[i] != header[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return format;
            }
        }
        return null;
    }

    /// <summary>
    /// 根据文件名获取对应的MIME类型
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".flac" => MIME_TYPES[AudioFormat.FLAC],
            ".mp3" => MIME_TYPES[AudioFormat.MP3],
            ".ogg" => MIME_TYPES[AudioFormat.OGG],
            ".m4a" => MIME_TYPES[AudioFormat.M4A],
            ".wav" => MIME_TYPES[AudioFormat.WAV],
            ".wma" => MIME_TYPES[AudioFormat.WMA],
            ".aac" => MIME_TYPES[AudioFormat.AAC],
            _ => "application/octet-stream",
        };
    }

    static readonly IReadOnlyDictionary<AudioFormat, byte[]> HEADERS = new Dictionary<AudioFormat, byte[]>
    {
        { AudioFormat.FLAC, "fLaC"u8.ToArray() }, // "fLaC"
        { AudioFormat.MP3, "ID3"u8.ToArray() }, // "ID3"
        { AudioFormat.OGG, "OggS"u8.ToArray() }, // "OggS"
        { AudioFormat.M4A, new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x7D } }, // "\x00\x00\x00\x1C ftyp"
        { AudioFormat.WAV, "RIFF"u8.ToArray() }, // "RIFF"
        { AudioFormat.WMA, new byte[] { 0x30, 0x26, 0xB2, 0x75 } }, // "\x30\x26\xB2\x75"
        { AudioFormat.AAC, new byte[] { 0xFF, 0xF1, 0x50 } }, // "\xFF\xF1\x50"
    };

    static readonly IReadOnlyDictionary<AudioFormat, string> MIME_TYPES = new Dictionary<AudioFormat, string>
    {
        { AudioFormat.FLAC, "audio/flac" },
        { AudioFormat.MP3, "audio/mpeg" },
        { AudioFormat.OGG, "audio/ogg" },
        { AudioFormat.M4A, "audio/mp4" },
        { AudioFormat.WAV, "audio/wav" },
        { AudioFormat.WMA, "audio/x-ms-wma" },
        { AudioFormat.AAC, "audio/aac" }
    };
}
