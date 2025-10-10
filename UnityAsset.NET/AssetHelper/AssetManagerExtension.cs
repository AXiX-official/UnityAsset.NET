using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.PreDefined;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

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
}