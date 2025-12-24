using AssetRipper.Tpk.TypeTrees;

namespace UnityAsset.NET.UnityTypeGen;

public static class TpkUnityTreeNodeFactory
{
    public static TpkUnityTreeNode?[] Cache = [];
    private static TpkTypeTreeBlob? _blob;

    public static void Init(TpkTypeTreeBlob blob)
    {
        _blob = blob;
        Cache = new TpkUnityTreeNode[blob.NodeBuffer.Count];
    }

    public static TpkUnityTreeNode Create(ushort index)
    {
        if (_blob == null)
            throw new InvalidOperationException("TpkUnityTreeNodeFactory is not initialized.");
        
        if (Cache[index] != null)
            return Cache[index]!;
        
        var node = _blob.NodeBuffer[index];
        var ret = new TpkUnityTreeNode
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

    public static void BuildMap()
    {
        CompactInPlace();
    }
}