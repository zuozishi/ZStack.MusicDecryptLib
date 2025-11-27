using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ZStack.MusicDecryptLib.Decrypters;

namespace ZStack.MusicDecryptLib;

public class AutoDecrypter
{
    private readonly Dictionary<Type, IDecrypter> _decrypters = [];

    public AutoDecrypter()
    {
        AddDecrypter(new NCM());
        AddDecrypter(new KGM());
    }

    public void AddKGG(string dbFilePath)
    {
        var kgg = new KGG();
        kgg.LoadKeyDic(dbFilePath);
        AddDecrypter(kgg);
    }

    public void AddDecrypter(IDecrypter decrypter)
    {
        _decrypters[decrypter.GetType()] = decrypter;
    }

    public IDecrypter GetDecrypter(Stream inputStream)
    {
        foreach (var decrypter in _decrypters.Values)
        {
            try
            {
                decrypter.CheckSupport(inputStream);
                return decrypter;
            }
            catch (Exception) { }
            inputStream.Position = 0;
        }
        throw new NotSupportedException("未匹配到可使用的解密器");
    }

    public bool TryGetDecrypter(Stream inputStream, [NotNullWhen(true)] out IDecrypter? decrypter)
    {
        foreach (var d in _decrypters.Values)
        {
            try
            {
                d.CheckSupport(inputStream);
                decrypter = d;
                return true;
            }
            catch (Exception) { }
            inputStream.Position = 0;
        }
        decrypter = null;
        return false;
    }
}
