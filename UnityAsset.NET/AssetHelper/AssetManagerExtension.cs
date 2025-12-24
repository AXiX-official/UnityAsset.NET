using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityAsset.NET.Enums;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.AssetHelper;

public static class AssetManagerExtension
{
    public static byte[] DecodeTexture2D(this AssetManager assetManager, ITexture2D tex)
    {
        var imageData = tex.image_data.size == 0 ? assetManager.LoadStreamingData(tex.m_StreamData) : tex.image_data.data;
        
        if (imageData == null)
            throw new NullReferenceException();
        
        var buildTarget = assetManager.BuildTarget;
        return TextureHelper.TextureHelper.Decode(imageData, tex.m_Width, tex.m_Height, (TextureFormat)tex.m_TextureFormat, buildTarget);
    }
    
    public static Image<Bgra32> DecodeTexture2DToImage(this AssetManager assetManager, ITexture2D tex, bool flip = true)
    {
        var imgData = assetManager.DecodeTexture2D(tex);
        var image = Image.LoadPixelData<Bgra32>(imgData, tex.m_Width, tex.m_Height);
        if (flip)
            image.Mutate(x => x.Flip(FlipMode.Vertical));
        return image;
    }
    
    public static Image<Bgra32> DecodeSpriteToImage(this AssetManager assetManager, ISprite sprite)
    {
        var image = SpriteHelper.GetImage(assetManager, sprite);
        return image;
    }
}