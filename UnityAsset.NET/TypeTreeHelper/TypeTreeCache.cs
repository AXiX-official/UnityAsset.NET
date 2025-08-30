using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper;

public static class TypeTreeCache
{
    private static readonly Dictionary<Hash128, List<TypeTreeNode>> Cache = new();
    
    private static readonly object CacheLock = new object();

    public static List<TypeTreeNode> GetOrAddNodes(Hash128 typeHash, List<TypeTreeNode> newNodes)
    {
        if (newNodes.Count == 0)
        {
            return newNodes;
        }
        
        lock (CacheLock)
        {
            if (Cache.TryGetValue(typeHash, out var cachedNodes))
            {
                return cachedNodes;
            }
            else
            {
                Cache.Add(typeHash, newNodes);
                return newNodes;
            }
        }
    }

    public static void CleanCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
        }
    }
}