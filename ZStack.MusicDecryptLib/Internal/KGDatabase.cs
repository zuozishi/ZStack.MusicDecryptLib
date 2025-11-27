using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ZStack.MusicDecryptLib.Internal;

internal class KGDatabase
{
    // SQLite 头（含尾部 0）
    private static readonly byte[] SqliteHeader =
    [
        (byte)'S', (byte)'Q' ,(byte)'L', (byte)'i', (byte)'t', (byte)'e', (byte)' ', (byte)'f',
        (byte)'o', (byte)'r' ,(byte)'m', (byte)'a', (byte)'t', (byte)' ', (byte)'3', 0x00
    ];

    private static readonly byte[] DefaultMasterKey =
    [
        0x1d, 0x61, 0x31, 0x45, 0xb2, 0x47, 0xbf, 0x7f, 0x3d, 0x18, 0x96, 0x72, 0x14, 0x4f, 0xe4, 0xbf
    ];

    private const int PageSize = 1024;

    private byte[] _db = [];

    // 公开获取解密后数据库镜像（只读）
    public ReadOnlyMemory<byte> DecryptedBytes => _db;

    public KGDatabase(string dbFilePath)
    {
        LoadFile(dbFilePath);
    }

    // 核心：加载并解密数据库
    private void LoadFile(string dbFilePath)
    {
        if (!File.Exists(dbFilePath))
            throw new MusicDecryptException("数据库文件不存在: " + dbFilePath);

        using var fs = new FileStream(dbFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long dbSize = fs.Length;
        if (dbSize % PageSize != 0)
            throw new MusicDecryptException("不受支持的文件: " + dbFilePath);

        int lastPage = (int)(dbSize / PageSize);
        fs.Seek(0, SeekOrigin.Begin);

        _db = new byte[dbSize];
        var pageBuffer = new byte[PageSize];
        var aesKey = new byte[16];
        var aesIv = new byte[16];
        long outOffset = 0;

        for (int pageNo = 1; pageNo <= lastPage; pageNo++, outOffset += PageSize)
        {
            ReadExactly(fs, pageBuffer, 0, PageSize);
            DerivePageKey(aesKey, aesIv, DefaultMasterKey, (uint)pageNo);

            if (pageNo == 1)
            {
                // 未加密数据库判定
                if (StartsWith(pageBuffer, SqliteHeader))
                {
                    Buffer.BlockCopy(pageBuffer, 0, _db, 0, PageSize);
                    // 直接读取剩余部分
                    int remain = (int)(dbSize - PageSize);
                    ReadExactly(fs, _db, PageSize, remain);
                    return;
                }

                // 校验页头合法性
                if (!IsValidPage1Header(pageBuffer))
                {
                    _db = [];
                    throw new MusicDecryptException("不受支持的文件: " + dbFilePath);
                }

                // 备份偏移16~23共8字节
                var backup8 = new byte[8];
                Buffer.BlockCopy(pageBuffer, 16, backup8, 0, 8);

                // 将偏移8~15复制到16~23
                Buffer.BlockCopy(pageBuffer, 8, pageBuffer, 16, 8);

                // 密文：跳过前 16 字节
                var cipherFirst = new byte[PageSize - 16];
                Buffer.BlockCopy(pageBuffer, 16, cipherFirst, 0, cipherFirst.Length);

                var plainFirst = AesCbcDecrypt(cipherFirst, aesKey, aesIv);

                // 写入 SQLite 头 & plaintext
                Buffer.BlockCopy(SqliteHeader, 0, _db, 0, SqliteHeader.Length);
                Buffer.BlockCopy(plainFirst, 0, _db, SqliteHeader.Length, plainFirst.Length);

                // 验证：前8字节匹配
                for (int i = 0; i < 8; i++)
                {
                    if (plainFirst[i] != backup8[i])
                        throw new MusicDecryptException("数据库解密失败: " + dbFilePath);
                }
            }
            else
            {
                // 整页密文
                var plainPage = AesCbcDecrypt(pageBuffer, aesKey, aesIv);
                Buffer.BlockCopy(plainPage, 0, _db, (int)outOffset, plainPage.Length);
            }
        }
    }

    public Dictionary<string, string> ReadKeyMap()
    {
        if (_db == null || _db.Length == 0)
            throw new MusicDecryptException("数据库未解密或解密失败");

        // 写入临时文件
        string tempPath = Path.Combine(Path.GetTempPath(), "kgdb_" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.WriteAllBytes(tempPath, _db);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = tempPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };
        var connString = builder.ToString();

        var localMap = new Dictionary<string, string>();
        try
        {
            using var conn = new SqliteConnection(connString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT EncryptionKeyId, EncryptionKey 
              FROM ShareFileItems
              WHERE EncryptionKeyId IS NOT NULL AND EncryptionKeyId != ''
                AND EncryptionKey IS NOT NULL AND EncryptionKey != '';";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string id = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(id)) continue;
                string key = reader.GetString(1);
                localMap[id] = key;
            }
        }
        finally
        {
            // 清理临时文件
            try { File.Delete(tempPath); } catch { /* 忽略 */ }
        }

        return localMap;
    }

    #region 内部辅助方法

    private static bool StartsWith(byte[] page, byte[] header)
    {
        if (header.Length > page.Length) return false;
        for (int i = 0; i < header.Length; i++)
            if (page[i] != header[i]) return false;
        return true;
    }

    private static void ReadExactly(Stream s, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = s.Read(buffer, offset + total, count - total);
            if (n == 0)
                throw new EndOfStreamException($"需要 {count} 字节但提前到达 EOF。");
            total += n;
        }
    }

    private static bool IsValidPage1Header(byte[] page1)
    {
        // 小端 32 位
        uint o10 = BitConverter.ToUInt32(page1, 16);
        uint o14 = BitConverter.ToUInt32(page1, 20);
        uint v6 = ((o10 & 0xFF) << 8) | ((o10 & 0xFF00) << 16);
        return (o14 == 0x20204000) &&
               (v6 - 0x200 <= 0xFE00) &&
               ((v6 & (v6 - 1)) == 0);
    }

    private static void DerivePageKey(byte[] aesKey, byte[] aesIv, byte[] masterKey, uint pageNo)
    {
        // 构造 24 字节 buffer
        var temp = new byte[24];
        Buffer.BlockCopy(masterKey, 0, temp, 0, 16);
        temp[16] = (byte)(pageNo);
        temp[17] = (byte)(pageNo >> 8);
        temp[18] = (byte)(pageNo >> 16);
        temp[19] = (byte)(pageNo >> 24);
        uint magic = 0x546C4173;
        temp[20] = (byte)(magic);
        temp[21] = (byte)(magic >> 8);
        temp[22] = (byte)(magic >> 16);
        temp[23] = (byte)(magic >> 24);

        Buffer.BlockCopy(MD5HashData(temp), 0, aesKey, 0, 16);

        // 生成 IV 源
        uint ebx = pageNo + 1;
        var ivSource = new byte[16];
        for (int i = 0; i < 16; i += 4)
        {
            const uint divisor = 0xCE26;
            uint quotient = ebx / divisor;
            uint eax = 0x7FFFFF07u * quotient;
            uint ecx = 0x9EF4u * ebx - eax;
            if ((ecx & 0x80000000u) != 0)
                ecx += 0x7FFFFF07u;
            ebx = ecx;
            ivSource[i] = (byte)(ebx);
            ivSource[i + 1] = (byte)(ebx >> 8);
            ivSource[i + 2] = (byte)(ebx >> 16);
            ivSource[i + 3] = (byte)(ebx >> 24);
        }

        Buffer.BlockCopy(MD5HashData(ivSource), 0, aesIv, 0, 16);
    }

    private static byte[] AesCbcDecrypt(byte[] cipher, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None; // 原实现不处理填充
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        // 输入必须是 16 字节对齐；若后续发现需要支持 PKCS7 可以调整
        if (cipher.Length % 16 != 0)
            throw new MusicDecryptException($"密文长度 {cipher.Length} 不是16字节对齐。");

        return TransformWhole(cipher, decryptor);
    }

    private static byte[] TransformWhole(byte[] data, ICryptoTransform transform)
    {
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static byte[] MD5HashData(byte[] bytes)
    {
        using var md5 = MD5.Create();
        return md5.ComputeHash(bytes);
    }

    #endregion
}
