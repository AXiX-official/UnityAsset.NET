using UnityAsset.NET.TypeTreeHelper.Specialized;

namespace UnityAsset.NET.Classes;

public interface ITexture2D : INamedAsset
{
    public Int32 m_Width { get; }
    public Int32 m_Height { get; }
    public Int32 m_TextureFormat { get; }
    public TypelessData image_data { get; }
    public List<byte> m_PlatformBlob { get; }
    public StreamingInfo m_StreamData { get; }
}