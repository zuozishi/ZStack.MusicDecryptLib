using System;
using System.IO;

namespace ZStack.MusicDecryptLib.Extensions;

internal static class StreamExtensions
{
    public static uint ReadUInt32(this Stream stream)
    {
        var bytes = new byte[4];
        int bytesRead = stream.Read(bytes, 0, 4);
        if (bytesRead < 4)
            throw new EndOfStreamException("无法读取足够的字节来构造UInt32");
        return BitConverter.ToUInt32(bytes, 0);
    }

    public static byte[] ReadChunk(this Stream stream)
    {
        uint len = stream.ReadUInt32();
        byte[] chunk = new byte[len];
        int bytesRead = stream.Read(chunk, 0, (int)len);
        if (bytesRead < len)
            throw new EndOfStreamException($"期望读取{len}字节，但只读取到{bytesRead}字节");
        return chunk;
    }
}
