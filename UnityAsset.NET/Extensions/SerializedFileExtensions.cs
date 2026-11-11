using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace UnityAsset.NET.Extensions;

public static class SerializedFileExtensions
{
    public static void ProcessAssetBundle(this SerializedFile sf)
    {
        foreach (var asset in sf.Assets)
        {
            switch (asset.TypeName)
            {
                case "AssetBundle":
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
                    break;
                }
                case "ResourceManager":
                {
                    IResourceManager resourceManager;
                    try
                    {
                        resourceManager = (IResourceManager)asset.Value;
                    }
                    catch (AssetUnavailableException)
                    {
                        // The session already reported why. Without the asset its container table is simply unknown, which
                        // costs the affected assets their paths and nothing else.
                        continue;
                    }

                    foreach (var (container, pptr) in resourceManager.m_Container)
                    {
                        sf.Containers[pptr.m_PathID] = container;
                    }
                    break;
                }
            }
        }
    }

    // Based on https://github.com/aelurum/AssetStudioMod
    public static void ReadExtractedTypeTree(this SerializedFile sf, IUnitySession session)
    {
        var extractedTypeTrees = sf.Metadata.ExtractedTypeTrees;
        if (extractedTypeTrees.Count == 0) return;
        var hashes = extractedTypeTrees.Keys.ToArray();
        foreach (var hash in hashes)
        {
            if (!session.TryGetLoadedFile(hash.ToString(), out var resourceFile)) continue;
            
            if (resourceFile is IReaderProvider readerProvider)
            {
                var reader = readerProvider.CreateReader(Endianness.LittleEndian);
                var serializedTypeList = extractedTypeTrees[hash];
                foreach (var serializedType in serializedTypeList)
                {
                    SerializedType.ReadTypeTreeBlob(reader, sf.Header.Version, out var nodes, out var stringBufferBytes);
                    serializedType.Nodes = nodes;
                    serializedType.StringBufferBytes = stringBufferBytes;
                    if (sf.Header.Version >= SerializedFileFormatVersion.SharedSubTreeSupport)
                    {
                        serializedType.SubTreeHashList = reader.ReadArray(r => new Hash128(r));
                    }
                }
                extractedTypeTrees[hash] = [];
            }
        }
        var notFound = extractedTypeTrees.Where(x => x.Value.Count != 0).ToArray();
        if (sf.Header.Version >= SerializedFileFormatVersion.SharedSubTreeSupport &&
            notFound.Length < extractedTypeTrees.Count)
        {
            sf.ReadExtractedSubTree(session);
        }
        extractedTypeTrees.Clear();
    }

    // Based on https://github.com/aelurum/AssetStudioMod
    private static void ReadExtractedSubTree(this SerializedFile sf, IUnitySession session)
    {
        var sharedSubTreeInfoDict = SerializedFileMetadata.SharedSubTreeInfoDict;
        var hashes = sharedSubTreeInfoDict.Keys.ToArray();
        foreach (var subTreeHash in hashes)
        {
            if (!session.TryGetLoadedFile(subTreeHash.ToString(), out var file))
                continue;

            if (file is IReaderProvider readerProvider)
            {
                var reader = readerProvider.CreateReader(Endianness.LittleEndian);
                sharedSubTreeInfoDict[subTreeHash] = SubTreeInfo.Parse(reader, sf.Header.Version);
            }
        }
        foreach (var serializedType in sf.Metadata.Types)
        {
            if (serializedType.SubTreeHashList.Length == 0)
                continue;

            serializedType.Nodes =
                SerializedFileMetadata.ResolveSubTrees(serializedType.Nodes, serializedType.SubTreeHashList);
        }
    }
}