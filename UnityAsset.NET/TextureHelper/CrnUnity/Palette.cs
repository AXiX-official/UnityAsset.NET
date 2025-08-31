// based on https://github.com/nesrak1/AssetsTools.NET/tree/dev/AssetsTools.NET.Texture/TextureDecoders

using UnityAsset.NET.IO;

namespace UnityAsset.NET.TextureHelper.CrnUnity;

public class Palette
{
    public uint Ofs;
    public uint Size;
    public ushort Num;

    public Palette(IReader reader)
    {
        Ofs = reader.ReadUInt24();
        Size = reader.ReadUInt24();
        Num = reader.ReadUInt16();
    }
}
