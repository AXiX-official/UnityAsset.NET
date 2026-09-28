using System.Collections.Concurrent;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Files.SerializedFiles;

public readonly record struct TypeTreeEntry(TypeTreeNode[] Nodes, int TypeId, TypeTreeRepr? Repr)
{
    public bool HasNodes => Nodes.Length > 0;
}

public static class TypeTreeIntern
{
    private static readonly ConcurrentDictionary<Hash128, TypeTreeEntry> Store = new();

    public static TypeTreeEntry GetOrAdd(Hash128 key, TypeTreeNode[] nodes, int typeId)
    {
        // Node-less: look up only. If a file with a real type tree already registered this hash, use that one.
        if (nodes.Length == 0)
            return Store.TryGetValue(key, out var known) ? known : new TypeTreeEntry(nodes, typeId, null);

        // Fast path: a node-bearing entry is already canonical, so there is nothing to build.
        if (Store.TryGetValue(key, out var existing) && existing.HasNodes)
            return existing;

        var candidate = new TypeTreeEntry(nodes, typeId, nodes[0].ToTypeTreeRepr(nodes));

        // Node-bearing wins; a node-less entry that is already stored gets upgraded. Losing the race to an equal
        // node-bearing entry is harmless: both are canonical and the node arrays are deduplicated by value later.
        return Store.AddOrUpdate(key, candidate, (_, old) => old.HasNodes ? old : candidate);
    }

    public static bool TryGet(Hash128 key, out TypeTreeEntry entry) => Store.TryGetValue(key, out entry);

    public static int Count => Store.Count;

    public static void Clear() => Store.Clear();
}