using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.Tests.Support;

/// <summary>
/// The two pieces of a type tree the Core IO tests still need: a distinct <see cref="Hash128"/> to key a
/// <see cref="SerializedType"/> by, and the raw node array a file carries for a type.
/// </summary>
internal static class TestCatalog
{
    /// <summary>
    /// A distinct type hash. <see cref="Hash128.GetHashCode"/> uses the first four bytes, so a repeated seed gives a
    /// distinct, well distributed key.
    /// </summary>
    public static Hash128 Hash(byte seed) => new(Enumerable.Repeat(seed, 16).ToArray());

    /// <summary>
    /// A type tree as the raw node array a file carries: index and level are what <c>ToTypeTreeRepr</c> walks to rebuild
    /// the tree, and each field gets an explicit Unity type (for example <c>int</c>).
    /// </summary>
    public static TypeTreeNode[] Nodes(string typeName, params (string Name, string Type)[] fields)
    {
        var nodes = new List<TypeTreeNode>
        {
            new(0, 0, default, 0, 0, 0, 0, 0) { Name = "m_Root", Type = typeName }
        };

        foreach (var (name, type) in fields)
        {
            nodes.Add(new TypeTreeNode(0, 1, default, 0, 0, 0, (uint)nodes.Count, 0)
            {
                Name = name,
                Type = type
            });
        }

        return nodes.ToArray();
    }
}