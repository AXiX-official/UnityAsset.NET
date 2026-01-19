using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTree;

public static class TypeTreeCache
{
    public static readonly Dictionary<Hash128, List<TypeTreeNode>> Cache = new();
    
    public static readonly Dictionary<Hash128, TypeTreeHelper.TypeTreeNode> Map = new();
    
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
    
    public static List<TypeTreeNode> GetOrAddNodes(Hash128 typeHash, TypeTreeHelper.TypeTreeNode newNodes)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(typeHash, out var cachedNodes))
            {
                return cachedNodes;
            }
            else
            {
                Cache.Add(typeHash, newNodes.Traverse().Select((x, index) => new TypeTreeNode(x.Node, index, x.Level)).ToList());
                return Cache[typeHash];
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

    private static TypeTreeHelper.TypeTreeNode ToTypeTreeHelperNode(TypeTreeNode current,
        List<TypeTreeNode> nodes)
    {
        return new TypeTreeHelper.TypeTreeNode
        {
            TypeName = current.Type,
            Name = current.Name,
            ByteSize = current.ByteSize,
            Version = (short)current.Version,
            TypeFlags = (byte)current.TypeFlags,
            MetaFlag = current.MetaFlags,
            SubNodes = current.Children(nodes).Select(child => ToTypeTreeHelperNode(child, nodes)).ToArray()
        };
    }
    
    public static IEnumerable<TypeTreeHelper.TypeTreeNode> ToTypeTreeHelperNodes()
    {
        lock (CacheLock)
        {
            foreach (var (key, nodes) in Cache)
            {
                if (!Map.ContainsKey(key))
                {
                    Map[key] = ToTypeTreeHelperNode(nodes[0], nodes);
                }
            }

            return Map.Values;
        }
    }
}