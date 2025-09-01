using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface ITexture2D : INamedAsset
{
    public Int32 m_Width { get; }
    public Int32 m_Height { get; }
    public UInt32 m_CompleteImageSize { get; }
    public Int32 m_TextureFormat { get; }
    public bool m_IsReadable { get; }
    public Int32 m_ImageCount { get; }
    public Int32 m_TextureDimension { get; }
    public IGLTextureSettings m_TextureSettings { get; }
    public Int32 m_LightmapFormat { get; }
    public Int32 m_ColorSpace { get; }
    public TypelessData image_data { get; }
    public StreamingInfo m_StreamData { get; }
}