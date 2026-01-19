using AssetRipper.Primitives;
using AssetRipper.Tpk.TypeTrees;

namespace UnityAsset.NET.TypeTreeHelper;

public static class TpkUnityTreeNodeFactory
{
    public static TypeTreeNode?[] Cache = [];
    private static TpkTypeTreeBlob? _blob;

    public static void Init(TpkTypeTreeBlob blob)
    {
        _blob = blob;
        Cache = new TypeTreeNode[blob.NodeBuffer.Count];
    }

    public static TypeTreeNode Create(ushort index)
    {
        if (_blob == null)
            throw new InvalidOperationException("TpkUnityTreeNodeFactory is not initialized.");
        
        if (Cache[index] != null)
            return Cache[index]!;
        
        var node = _blob.NodeBuffer[index];
        var ret = new TypeTreeNode
        {
            Index = index,
            TypeName = _blob.StringBuffer[node.TypeName],
            Name = _blob.StringBuffer[node.Name],
            ByteSize = node.ByteSize,
            Version = node.Version,
            TypeFlags = node.TypeFlags,
            MetaFlag = node.MetaFlag,
            SubNodes = node.SubNodes.Select(Create).ToArray()
        };
        Cache[index] = ret;
        return ret;
    }
    
    public static void CompactInPlace()
    {
        int writeIndex = 0;
    
        for (int readIndex = 0; readIndex < Cache.Length; readIndex++)
        {
            var node = Cache[readIndex];
            
            if (node is null) continue;
            
            Cache[writeIndex] = node;
            node.Index = writeIndex;
            writeIndex++;
        }
    
        Array.Resize(ref Cache, writeIndex);
    }
    
    public static Dictionary<string, List<TypeTreeNode>> GetRootTypeNodesAfterVersion(string minimalVersionStr)
    {
        if (_blob is null)
            throw new InvalidOperationException("TpkUnityTreeNodeFactory is not initialized.");
        
        UnityVersion.TryParse(minimalVersionStr, out var minimalVersion, out _);
        
        Dictionary<string, HashSet<ushort>> rootTypeNodesMap = new();
        foreach (var info in _blob.ClassInformation)
        {
            bool isSupportedVersion = false;
            // versions are sorted 
            for (int i = 0; i < info.Classes.Count; i++)
            {
                var (_, @class) = info.Classes[i];
                if (!isSupportedVersion && i < info.Classes.Count - 1)
                {
                    var (nextVersion, _) = info.Classes[i + 1];
                    if (minimalVersion < nextVersion)
                    {
                        isSupportedVersion = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (@class is null)
                    continue;
                
                var name = _blob.StringBuffer[@class.Name];

                if ((@class.Flags & TpkUnityClassFlags.HasReleaseRootNode) == 0)
                    continue;
                
                if (!rootTypeNodesMap.ContainsKey(name))
                    rootTypeNodesMap[name] = new HashSet<ushort>();
                
                rootTypeNodesMap[name].Add(@class.ReleaseRootNode);
            }
        }

        return rootTypeNodesMap.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value.Select(Create).ToList()
        );
    }
    
    public static Dictionary<string, TypeTreeNode> GetRootTypeNodes(string versionStr)
    {
        if (_blob is null)
            throw new InvalidOperationException("TpkUnityTreeNodeFactory is not initialized.");
        
        UnityVersion.TryParse(versionStr, out var version, out _);
        
        Dictionary<string, ushort> rootTypeNodesMap = new();
        foreach (var info in _blob.ClassInformation)
        {
            bool isSupportedVersion = false;
            // versions are sorted 
            for (int i = 0; i < info.Classes.Count; i++)
            {
                var (_, @class) = info.Classes[i];
                if (!isSupportedVersion && i < info.Classes.Count - 1)
                {
                    var (nextVersion, _) = info.Classes[i + 1];
                    if (version < nextVersion)
                    {
                        isSupportedVersion = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (@class is null)
                    continue;
                
                var name = _blob.StringBuffer[@class.Name];

                if ((@class.Flags & TpkUnityClassFlags.HasReleaseRootNode) == 0)
                    continue;
                
                rootTypeNodesMap[name] = @class.ReleaseRootNode;
                
                if (isSupportedVersion)
                    break;
            }
        }

        return rootTypeNodesMap.ToDictionary(
            kvp => kvp.Key, 
            kvp => Create(kvp.Value)
        );
    }
}