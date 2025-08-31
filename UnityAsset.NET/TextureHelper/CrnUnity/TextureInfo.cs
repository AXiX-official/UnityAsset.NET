// based on https://github.com/nesrak1/AssetsTools.NET/tree/dev/AssetsTools.NET.Texture/TextureDecoders
namespace UnityAsset.NET.TextureHelper.CrnUnity;

public class TextureInfo
{
    public uint StructSize;
    public uint Width;
    public uint Height;
    public uint Levels;
    public uint Faces;
    public uint BytesPerBlock;
    public uint Userdata0;
    public uint Userdata1;
    public Format Format;
}
