using System;

namespace ZStack.MusicDecryptLib.Internal;

// 工具：Endian Big-Endian 读写
internal static class Endian
{
    public static ulong BeReadUInt64(ReadOnlySpan<byte> src)
    {
        if (src.Length < 8) throw new ArgumentException("Need 8 bytes");
        return ((ulong)src[0] << 56) |
               ((ulong)src[1] << 48) |
               ((ulong)src[2] << 40) |
               ((ulong)src[3] << 32) |
               ((ulong)src[4] << 24) |
               ((ulong)src[5] << 16) |
               ((ulong)src[6] << 8) |
               (ulong)src[7];
    }

    public static void BeWriteUInt64(Span<byte> dst, ulong value)
    {
        if (dst.Length < 8) throw new ArgumentException("Need 8 bytes");
        dst[0] = (byte)(value >> 56);
        dst[1] = (byte)(value >> 48);
        dst[2] = (byte)(value >> 40);
        dst[3] = (byte)(value >> 32);
        dst[4] = (byte)(value >> 24);
        dst[5] = (byte)(value >> 16);
        dst[6] = (byte)(value >> 8);
        dst[7] = (byte)value;
    }
}

// TEA (16轮) + 自定义CBC解密
internal static class Tea
{
    private const int Rounds = 16;
    private const uint Delta = 0x9e3779b9;
    // 将常量表达式的乘法强制转换为 unchecked 上下文，防止编译时溢出
    private const uint ExpectedSum = unchecked(Rounds * Delta);
    private const int BlockSize = 8;
    private const int FixedSaltLen = 2;
    private const int ZeroPadLen = 7;

    private static uint SingleRound(uint value, uint sum, uint k1, uint k2)
    {
        return ((value << 4) + k1) ^ (value + sum) ^ ((value >> 5) + k2);
    }

    private static ulong EcbDecrypt(ulong value, ReadOnlySpan<uint> key)
    {
        uint y = (uint)(value >> 32);
        uint z = (uint)value;
        uint sum = ExpectedSum;

        for (int i = 0; i < Rounds; i++)
        {
            z -= SingleRound(y, sum, key[2], key[3]);
            y -= SingleRound(z, sum, key[0], key[1]);
            sum -= Delta;
        }
        return ((ulong)y << 32) | z;
    }

    private static void DecryptRound(Span<byte> plainOut, ReadOnlySpan<byte> cipherIn,
                                     ref ulong iv1, ref ulong iv2, ReadOnlySpan<uint> key)
    {
        ulong iv1Next = Endian.BeReadUInt64(cipherIn);
        ulong iv2Next = EcbDecrypt(iv1Next ^ iv2, key);
        ulong plain = iv2Next ^ iv1;
        iv1 = iv1Next;
        iv2 = iv2Next;
        Endian.BeWriteUInt64(plainOut, plain);
    }

    // 对应 C++: TEA::tc_tea_cbc_decrypt
    public static byte[] TcTeaCbcDecrypt(ReadOnlySpan<byte> cipher, ReadOnlySpan<uint> key)
    {
        if (cipher.Length % BlockSize != 0 || cipher.Length < BlockSize * 2)
            return Array.Empty<byte>();

        ulong iv1 = 0;
        ulong iv2 = 0;
        Span<byte> header = stackalloc byte[BlockSize * 2];

        var cursor = cipher;
        DecryptRound(header.Slice(0, BlockSize), cursor.Slice(0, BlockSize), ref iv1, ref iv2, key);
        cursor = cursor.Slice(BlockSize);
        DecryptRound(header.Slice(BlockSize, BlockSize), cursor.Slice(0, BlockSize), ref iv1, ref iv2, key);
        cursor = cursor.Slice(BlockSize);

        int hdrSkipLen = 1 + (header[0] & 7) + FixedSaltLen;
        int realPlainLen = cipher.Length - hdrSkipLen - ZeroPadLen;
        if (realPlainLen <= 0)
            return Array.Empty<byte>();

        byte[] result = new byte[realPlainLen];
        int copied = 0;

        int headerRemain = header.Length - hdrSkipLen;
        int copyLen = Math.Min(headerRemain, realPlainLen);
        if (copyLen > 0)
        {
            header.Slice(hdrSkipLen, copyLen).CopyTo(result.AsSpan(0, copyLen));
            copied += copyLen;
        }

        int remaining = realPlainLen - copied;
        if (remaining > 0)
        {
            // 将 stackalloc 移出循环
            Span<byte> plainBlock = stackalloc byte[BlockSize];
            int tailBlocks = (cipher.Length / BlockSize) - 3;
            for (int bi = 0; bi < tailBlocks && remaining > 0; bi++)
            {
                DecryptRound(plainBlock, cursor.Slice(0, BlockSize), ref iv1, ref iv2, key);
                cursor = cursor.Slice(BlockSize);
                int take = Math.Min(BlockSize, remaining);
                plainBlock.Slice(0, take).CopyTo(result.AsSpan(copied, take));
                copied += take;
                remaining -= take;
            }

            if (remaining > 0)
            {
                // 最后再解一次 header第二部分逻辑：decrypt_round(header + blockSize,...)
                Span<byte> lastBlock = stackalloc byte[BlockSize];
                DecryptRound(lastBlock, cursor.Slice(0, BlockSize), ref iv1, ref iv2, key);
                // 只取一个字节：p_output[0] = header[kTeaBlockSize];
                result[copied] = lastBlock[0];
                // copied++ (可选，不再有后续)
            }
        }

        // 零填充校验（原实现注释掉）
        return result;
    }
}

// RC4 变体（状态长度 = key 长度）
internal sealed class Rc4Variant
{
    private readonly byte[] _state;
    private int _i;
    private int _j;
    private readonly int _n;

    public Rc4Variant(ReadOnlySpan<byte> key)
    {
        _state = new byte[key.Length];
        for (int i = 0; i < key.Length; i++)
            _state[i] = (byte)i;

        int j = 0;
        for (int i = 0; i < key.Length; i++)
        {
            j = (j + _state[i] + key[i]) % key.Length;
            (_state[i], _state[j]) = (_state[j], _state[i]);
        }
        _n = key.Length;
        _i = 0;
        _j = 0;
    }

    public void Derive(Span<byte> buffer)
    {
        int i = _i;
        int j = _j;
        var s = _state;
        int n = _n;

        for (int k = 0; k < buffer.Length; k++)
        {
            i = (i + 1) % n;
            j = (j + s[i]) % n;
            (s[i], s[j]) = (s[j], s[i]);
            int idx = (s[i] + s[j]) % n;
            buffer[k] ^= s[idx];
        }

        _i = i;
        _j = j;
    }
}

// Ekey 处理
internal static class Ekey
{
    private const string EKeyV2Prefix = "UVFNdXNpYyBFbmNWMixLZXk6";
    private static readonly byte[] EKeyV2Key1 =
    {
            0x33,0x38,0x36,0x5A,0x4A,0x59,0x21,0x40,0x23,0x2A,0x24,0x25,0x5E,0x26,0x29,0x28
        };
    private static readonly byte[] EKeyV2Key2 =
    {
            0x2A,0x2A,0x23,0x21,0x28,0x23,0x24,0x25,0x26,0x5E,0x61,0x31,0x63,0x5A,0x2C,0x54
        };

    public static byte[] Decrypt(string ekey)
    {
        if (ekey.StartsWith(EKeyV2Prefix, StringComparison.Ordinal))
        {
            string stripped = ekey.Substring(EKeyV2Prefix.Length);
            return DecryptV2(stripped);
        }
        return DecryptV1(ekey);
    }

    private static byte[] DecryptV2(string ekey)
    {
        // 第一层
        var first = Tea.TcTeaCbcDecrypt(StringToSpan(ekey), BytesToUInt32Span(EKeyV2Key1));
        // 第二层
        var second = Tea.TcTeaCbcDecrypt(first, BytesToUInt32Span(EKeyV2Key2));
        // 然后进入 v1 流程（需要把 second 视为 base64 字符串? 原 C++: span2ss -> decrypt_ekey_v1）
        // 原代码: result = TEA decrypt -> TEA decrypt -> decrypt_ekey_v1(span2ss(result))
        // 此处 second 中是原始字节并被 reinterpret_cast 为 string_view(按字节). 按原语义直接转为 ISO-8859-1 字符串。
        string pseudo = BytesToLatin1(second);
        return DecryptV1(pseudo);
    }

    private static byte[] DecryptV1(string ekey)
    {
        // Base64 解码
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(ekey);
        }
        catch
        {
            return Array.Empty<byte>();
        }
        if (decoded.Length < 8)
            return Array.Empty<byte>();

        // 组装 TEA key (4 * uint32)
        uint[] teaKey =
        {
                (uint)(0x69005600 | (decoded[0] << 16) | decoded[1]),
                (uint)(0x46003800 | (decoded[2] << 16) | decoded[3]),
                (uint)(0x2b002000 | (decoded[4] << 16) | decoded[5]),
                (uint)(0x15000b00 | (decoded[6] << 16) | decoded[7])
            };

        var cipherSpan = decoded.AsSpan(8);
        var plain = Tea.TcTeaCbcDecrypt(cipherSpan, teaKey);

        if (plain.Length == 0)
            return Array.Empty<byte>();

        // 拼接前8字节 + 解密结果
        byte[] result = new byte[8 + plain.Length];
        Buffer.BlockCopy(decoded, 0, result, 0, 8);
        Buffer.BlockCopy(plain, 0, result, 8, plain.Length);
        return result;
    }

    private static ReadOnlySpan<byte> StringToSpan(string s) => System.Text.Encoding.ASCII.GetBytes(s);
    private static ReadOnlySpan<uint> BytesToUInt32Span(byte[] keyBytes)
    {
        if (keyBytes.Length != 16) throw new ArgumentException("Key must be 16 bytes.");
        // 按小端还是大端？C++ reinterpret_cast<uint32_t*> 取本机端序（通常小端）
        // 保持与 C++ 一致：按系统小端
        uint[] key = new uint[4];
        for (int i = 0; i < 4; i++)
        {
            key[i] = BitConverter.ToUInt32(keyBytes, i * 4); // 小端
        }
        return key;
    }

    private static string BytesToLatin1(ReadOnlySpan<byte> data)
    {
        char[] chars = new char[data.Length];
        for (int i = 0; i < data.Length; i++)
            chars[i] = (char)data[i];
        return new string(chars);
    }
}

// 抽象基类
internal abstract class Qmc2Base
{
    public abstract void Decrypt(Span<byte> data, long offset);
}

// MAP 实现
internal sealed class Qmc2Map : Qmc2Base
{
    private const int MapOffsetBoundary = 0x7FFF;
    private const int MapIndexOffset = 71214;
    private const int MapKeySize = 128;
    private readonly byte[] _mapKey = new byte[MapKeySize];

    public Qmc2Map(ReadOnlySpan<byte> key)
    {
        int n = key.Length;
        for (int i = 0; i < MapKeySize; i++)
        {
            int j = (int)((i * (long)i + MapIndexOffset) % n);
            int shift = (j + 4) % 8;
            _mapKey[i] = (byte)((key[j] << shift) | (key[j] >> shift));
        }
    }

    public override void Decrypt(Span<byte> data, long offset)
    {
        for (int i = 0; i < data.Length; i++)
        {
            long idx = offset <= MapOffsetBoundary ? offset : (offset % MapOffsetBoundary);
            data[i] ^= _mapKey[idx % _mapKey.Length];
            offset++;
        }
    }
}

// RC4 实现
internal sealed class Qmc2Rc4 : Qmc2Base
{
    private const int FirstSegmentSize = 0x80;
    private const int OtherSegmentSize = 0x1400;
    private const int Rc4StreamSize = OtherSegmentSize + 512;

    private readonly byte[] _key;
    private readonly double _hash;
    private readonly byte[] _rc4Stream;

    public Qmc2Rc4(ReadOnlySpan<byte> keyBytes)
    {
        _key = keyBytes.ToArray();
        _hash = ComputeHash(_key);

        _rc4Stream = new byte[Rc4StreamSize];
        // RC4 流初始化：与 C++ 中 Derive(buffer) 相同，这里 buffer 初值全 0
        var rc4 = new Rc4Variant(_key);
        rc4.Derive(_rc4Stream);
    }

    public override void Decrypt(Span<byte> data, long offset)
    {
        // 仿照原代码分段
        if (offset < FirstSegmentSize)
        {
            int n = DecryptFirstSegment(data, offset);
            offset += n;
            data = data.Slice(n);
        }

        while (!data.IsEmpty)
        {
            int n = DecryptOtherSegment(data, offset);
            offset += n;
            data = data.Slice(n);
        }
    }

    private static double ComputeHash(byte[] key)
    {
        uint h = 1;
        foreach (var b in key)
        {
            if (b == 0) continue;
            uint next = h * b;
            if (next <= h) break; // overflow 或停止条件
            h = next;
        }
        return (double)h;
    }

    private static long GetSegmentKey(double keyHash, long segmentId, byte seed)
    {
        if (seed == 0) return 0;
        double result = keyHash / (seed * (segmentId + 1.0)) * 100.0;
        return (long)result;
    }

    private int DecryptFirstSegment(Span<byte> data, long offset)
    {
        int nKey = _key.Length;
        int processLen = (int)Math.Min(data.Length, FirstSegmentSize - offset);
        for (int i = 0; i < processLen; i++)
        {
            long idx = GetSegmentKey(_hash, offset, _key[offset % nKey]) % nKey;
            data[i] ^= _key[idx];
            offset++;
        }
        return processLen;
    }

    private int DecryptOtherSegment(Span<byte> data, long offset)
    {
        int nKey = _key.Length;
        long segmentIdx = offset / OtherSegmentSize;
        long segmentOffset = offset % OtherSegmentSize;

        long skipLen = GetSegmentKey(_hash, segmentIdx, _key[segmentIdx % nKey]) & 0x1FF;
        int processLen = (int)Math.Min(data.Length, OtherSegmentSize - segmentOffset);
        int start = (int)(skipLen + segmentOffset);

        // 注意：start + processLen 不能超过 rc4Stream 长度
        if (start + processLen > _rc4Stream.Length)
            processLen = _rc4Stream.Length - start;

        for (int i = 0; i < processLen; i++)
        {
            data[i] ^= _rc4Stream[start + i];
        }
        return processLen;
    }
}

// 工厂类
internal static class Qmc2Factory
{
    public static Qmc2Base? Create(string ekey)
    {
        var key = Ekey.Decrypt(ekey);
        if (key == null || key.Length == 0)
            return null;

        if (key.Length < 300)
            return new Qmc2Map(key);
        return new Qmc2Rc4(key);
    }
}
