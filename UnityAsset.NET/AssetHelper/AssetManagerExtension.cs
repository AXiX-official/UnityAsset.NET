using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace UnityAsset.NET.AssetHelper;

public static class AssetManagerExtension
{
    /// <summary>Decodes a texture to raw bytes, reading its streaming data from the session when it is not inline.</summary>
    public static byte[] DecodeTexture2D(this IUnitySession session, ITexture2D tex)
    {
        var imageData = tex.image_data.size == 0 ? session.LoadStreamingData(tex.m_StreamData) : tex.image_data.data;
        
        if (imageData == null)
            throw new NullReferenceException();
        
        var buildTarget = session.BuildTarget;
        return TextureHelper.TextureHelper.Decode(imageData, tex.m_Width, tex.m_Height, (TextureFormat)tex.m_TextureFormat, buildTarget);
    }
    
    /// <summary>Decodes a texture into an image, optionally flipping it the way Unity stores it.</summary>
    public static Image<Bgra32> DecodeTexture2DToImage(this IUnitySession session, ITexture2D tex, bool flip = true)
    {
        var imgData = session.DecodeTexture2D(tex);
        var image = Image.LoadPixelData<Bgra32>(imgData, tex.m_Width, tex.m_Height);
        if (flip)
            image.Mutate(x => x.Flip(FlipMode.Vertical));
        return image;
    }
    
    /// <summary>Decodes the texture a sprite points at, cropped to the sprite rectangle.</summary>
    public static Image<Bgra32> DecodeSpriteToImage(this IUnitySession session, ISprite sprite)
    {
        var image = SpriteHelper.GetImage(session, sprite);
        return image;
    }
}