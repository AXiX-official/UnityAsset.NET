using System.IO;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO.Reader;

// based on https://github.com/nesrak1/AssetsTools.NET/tree/dev/AssetsTools.NET.Texture/TextureDecoders
namespace UnityAsset.NET.TextureHelper.CrnUnity;

public static class Info
{
    public static Header GetHeader(byte[] data)
    {
        if (data.Length < Consts.HEADER_MIN_SIZE)
            throw new Exception("data is too small");

        var reader = new MemoryReader(data);
        reader.Endian = Endianness.BigEndian;

        var fileHeader = new Header(reader);
        if (fileHeader.Sig != Consts.SIG_VALUE)
            throw new Exception("Invalid file header");

        if (fileHeader.HeaderSize < Consts.HEADER_MIN_SIZE || data.Length < fileHeader.HeaderSize)
            throw new Exception("data is too small");

        return fileHeader;
    }
}
