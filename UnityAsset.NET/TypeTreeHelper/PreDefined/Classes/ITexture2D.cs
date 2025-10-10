using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface ITexture2D : INamedAsset
{
    public Int32? m_ForcedFallbackFormat { get; }

    public bool? m_DownscaleFallback { get; }

    public bool? m_IsAlphaChannelOptional { get; }
    public Int32 m_Width { get; }
    public Int32 m_Height { get; }
    public UInt32 m_CompleteImageSize { get; }
    public Int32? m_MipsStripped { get; }
    public Int32 m_TextureFormat { get; }
    public Int32 m_MipCount { get; }
    public bool m_IsReadable { get; }
    public bool? m_IsPreProcessed { get; }
    public bool? m_IgnoreMipmapLimit { get; }
    public string? m_MipmapLimitGroupName { get; }
    public bool? m_StreamingMipmaps { get; }
    public Int32? m_StreamingMipmapsPriority { get; }
    public Int32 m_ImageCount { get; }
    public Int32 m_TextureDimension { get; }
    public IGLTextureSettings m_TextureSettings { get; }
    public Int32 m_LightmapFormat { get; }
    public Int32 m_ColorSpace { get; }
    public List<byte>? m_PlatformBlob { get; }
    public TypelessData image_data { get; }
    public StreamingInfo m_StreamData { get; }
}