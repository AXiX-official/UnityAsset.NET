using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace UnityAsset.NET.Extensions;

public static class SerializedFileExtensions
{
    public static void ProcessAssetBundle(this SerializedFile sf)
    {
        foreach (var asset in sf.Assets)
        {
            if (asset.TypeName == "AssetBundle")
            {
                IAssetBundle assetBundle;
                try
                {
                    assetBundle = (IAssetBundle)asset.Value;
                }
                catch (AssetUnavailableException)
                {
                    // The session already reported why. Without the asset its container table is simply unknown, which
                    // costs the affected assets their paths and nothing else.
                    continue;
                }

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