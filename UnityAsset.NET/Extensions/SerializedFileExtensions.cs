using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.Extensions;

public static class SerializedFileExtensions
{
    public static void ProcessAssetBundle(this SerializedFile sf)
    {
        foreach (var asset in sf.Assets)
        {
            if (asset.Type == "AssetBundle")
            { 
                var assetBundle = (IAssetBundle)asset.Value;
                
                foreach (var (container, assetInfo) in assetBundle.m_Container)
                {
                    var preloadIndex = assetInfo.preloadIndex;
                    var preloadSize = assetInfo.preloadSize;
                    for (int i = preloadIndex; i < preloadIndex + preloadSize; i++)
                    {
                        var pptr = assetBundle.m_PreloadTable[i];
                        sf.Containers[pptr.m_PathID] = container;
                    }
                }
            }
        }
    }
}