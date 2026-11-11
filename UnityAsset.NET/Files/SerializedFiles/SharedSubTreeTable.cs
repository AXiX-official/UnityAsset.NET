using System.Collections.Concurrent;

namespace UnityAsset.NET.Files.SerializedFiles;

/// <summary>
/// The shared sub-trees one load knows about.
/// <para>
/// A serialized file may carry a table of sub-tree hashes and declare types whose trees are references into it, and a
/// type tree whose blob was extracted refers to a table that lives in <em>another</em> file — so the entries have to
/// outlive the file they were read from. What they must not do is outlive the session: a hash is only meaningful inside
/// one game's build, and two sessions reading two games would otherwise see each other's trees, with the second one
/// either resolving against the wrong shape or failing to resolve at all.
/// </para>
/// <para>
/// Writes are concurrent because a session parses its files in parallel; reads are takes on that same table.
/// </para>
/// </summary>
public sealed class SharedSubTreeTable
{
    private readonly ConcurrentDictionary<Hash128, SubTreeInfo?> _entries = new();

    public int Count => _entries.Count;

    /// <summary>A snapshot of the hashes, so a caller can add entries while walking it.</summary>
    public IReadOnlyCollection<Hash128> Hashes => _entries.Keys.ToArray();

    public bool TryAdd(Hash128 hash, SubTreeInfo? info) => _entries.TryAdd(hash, info);

    /// <summary>Adds or replaces, for the pass that fills a table entry whose blob was extracted elsewhere.</summary>
    public void Set(Hash128 hash, SubTreeInfo? info) => _entries[hash] = info;

    public bool TryGet(Hash128 hash, out SubTreeInfo? info) => _entries.TryGetValue(hash, out info);

    public void Clear() => _entries.Clear();
}
