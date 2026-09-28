using AssetRipper.Tpk.TypeTrees;

namespace UnityAsset.NET.TypeTreeHelper;

/// <summary>
/// Turns a node index in a tpk database into an interned type tree, memoized per node so a subtree is walked once per
/// database instead of once per caller.
/// <para>
/// Shared by the two things that read a tpk: <see cref="TpkTypeTreeCatalog"/>, which resolves the root trees of
/// stripped files at runtime, and <see cref="TpkUnityTreeNodeFactory"/>, which feeds the offline interface generator.
/// They differ in which roots they want — one version's roots versus every class's roots across all versions — but the
/// interning itself is the same walk over the same node buffer, and it used to exist twice. The <c>0x4000</c> meta
/// flag in particular is a detail that should not be spelled out in two places.
/// </para>
/// </summary>
internal static class TpkNodeInterner
{
    /// <summary>Meta flag marking a node whose reader has to align the stream.</summary>
    private const int AlignMetaFlag = 0x4000;

    /// <summary>
    /// Interning the type tree at one database node. Results are stored in <paramref name="memo"/>, which the caller
    /// owns: it is indexed by node index, so it must not be compacted or reordered while it is in use.
    /// </summary>
    /// <param name="blob">The database being read.</param>
    /// <param name="index">Index of the node in the database's node buffer.</param>
    /// <param name="memo">One slot per database node, or null to not reuse an earlier result.</param>
    internal static TypeTreeRepr Create(TpkTypeTreeBlob blob, ushort index, TypeTreeRepr?[]? memo = null)
    {
        if (memo is not null && memo[index] is { } cached)
            return cached;

        var node = blob.NodeBuffer[index];
        var repr = TypeTreeRepr.Create(
            blob.StringBuffer[node.Name],
            blob.StringBuffer[node.TypeName],
            node.SubNodes.Select(child => Create(blob, child, memo)).ToArray(),
            (node.MetaFlag & AlignMetaFlag) != 0);

        if (memo is not null)
            memo[index] = repr;

        return repr;
    }
}