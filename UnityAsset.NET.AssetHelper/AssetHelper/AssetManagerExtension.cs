using SkiaSharp;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace UnityAsset.NET.AssetHelper;

public static class AssetManagerExtension
{
    /// <summary>Decodes a texture to raw bytes, reading its streaming data from the session when it is not inline.</summary>
    public static byte[] DecodeTexture2D(this IUnitySession session, ITexture2D tex, IVirtualFileInfo? src)
    {
        var imageData = tex.image_data.size == 0 ? session.LoadStreamingData(tex.m_StreamData, src) : tex.image_data.data;
        
        if (imageData == null)
            throw new NullReferenceException();
        
        var buildTarget = session.BuildTarget;
        return TextureHelper.TextureHelper.Decode(imageData, tex.m_Width, tex.m_Height, (TextureFormat)tex.m_TextureFormat, buildTarget);
    }
    
    /// <summary>Decodes a texture into a bitmap, optionally flipping it the way Unity stores it.</summary>
    public static SKBitmap DecodeTexture2DToBitmap(this IUnitySession session, ITexture2D tex, IVirtualFileInfo? src, bool flip = true)
    {
        var imgData = session.DecodeTexture2D(tex, src);
        return BitmapHelper.FromBytes(imgData, tex.m_Width, tex.m_Height, flip);
    }
    
    /// <summary>Decodes the texture a sprite points at, cropped to the sprite rectangle.</summary>
    public static SKBitmap DecodeSpriteToImage(this IUnitySession session, ISprite sprite, IVirtualFileInfo? src)
        => SpriteHelper.GetImage(session, sprite, src);
}
