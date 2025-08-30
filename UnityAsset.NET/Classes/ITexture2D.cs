using UnityAsset.NET.TypeTreeHelper.Specialized;

namespace UnityAsset.NET.Classes;

public interface ITexture2D : INamedAsset
{
    public Int32 m_Width { get; }
    public Int32 m_Height { get; }
    public UInt32 m_CompleteImageSize { get; }
    public Int32 m_TextureFormat { get; }
    public StreamingInfo m_StreamData { get; }
}