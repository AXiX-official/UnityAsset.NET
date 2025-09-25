
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper.CodeGeneration;

public static class TypeCollector
{
    public static Dictionary<int, BaseTypeInfo> Collect(IEnumerable<SerializedType> serializedTypes)
    {
        var typeCache = new Dictionary<int, BaseTypeInfo>();
        foreach (var serializedType in serializedTypes)
        {
            if (serializedType.Nodes.Count == 0)
                continue;
            
            var rootNode = serializedType.Nodes[0];
            var hash = rootNode.GetHashCode(serializedType.Nodes);
            if (typeCache.ContainsKey(hash))
            {
                continue;
            }

            var typeResolver = new CSharpTypeResolver(serializedType.Nodes, typeCache);

            typeResolver.Resolve(serializedType.Nodes[0]);
        }

        return typeCache;
    }
}
