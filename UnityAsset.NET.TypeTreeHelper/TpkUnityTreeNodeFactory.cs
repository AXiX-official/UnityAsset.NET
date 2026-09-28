using AssetRipper.Primitives;
using AssetRipper.Tpk.TypeTrees;

namespace UnityAsset.NET.TypeTreeHelper;

/// <summary>
/// Reads root type trees out of a tpk database for the offline interface generator. The runtime does not use this
/// class: a session opens a <see cref="TpkTypeTreeCatalog"/> instead, which shares one database between sessions and
/// releases it when the last one is done.
/// <para>
/// The two differ in what they ask for, which is why they are not one class: this one wants every class's roots across
/// every version, because it generates an interface per class per version; the runtime wants one version's roots,
/// because a file declares the version it was built with. The node interning they share lives in
/// <see cref="TpkNodeInterner"/>.
/// </para>
/// </summary>
public static class TpkUnityTreeNodeFactory
{
    private static TypeTreeRepr?[] Cache = [];
    private static TpkTypeTreeBlob? _blob;

    public static void Init(TpkTypeTreeBlob blob)
    {
        _blob = blob;
        Cache = new TypeTreeRepr[blob.NodeBuffer.Count];
    }

    public static Dictionary<string, List<(UnityVersion, TypeTreeRepr)>> GetRootTypeNodesAfterVersion(string minimalVersionStr)
    {
        if (_blob is null)
            throw new InvalidOperationException("TpkUnityTreeNodeFactory is not initialized.");

        UnityVersion.TryParse(minimalVersionStr, out var minimalVersion, out _);

        Dictionary<string, HashSet<(UnityVersion version, ushort index)>> rootTypeNodesMap = new();
        foreach (var info in _blob.ClassInformation)
        {
            bool isSupportedVersion = false;
            // versions are sorted
            for (int i = 0; i < info.Classes.Count; i++)
            {
                var (currentVersion, @class) = info.Classes[i];
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
                    rootTypeNodesMap[name] = new();

                rootTypeNodesMap[name].Add((currentVersion, @class.ReleaseRootNode));
            }
        }

        return rootTypeNodesMap.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(v => (v.version, TpkNodeInterner.Create(_blob, v.index, Cache))).ToList()
        );
    }
}