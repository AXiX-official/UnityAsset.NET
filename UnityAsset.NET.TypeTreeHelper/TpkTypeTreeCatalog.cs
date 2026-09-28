using System.Collections.Concurrent;
using AssetRipper.Primitives;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;

namespace UnityAsset.NET.TypeTreeHelper;

public sealed class TpkTypeTreeCatalog : IDisposable
{
    /// <summary>The shared state behind every handle for one database path.</summary>
    private sealed class Database
    {
        public Database(TpkTypeTreeBlob blob)
        {
            Blob = blob;
            Nodes = new TypeTreeRepr?[blob.NodeBuffer.Count];
            RootsByVersion = new ConcurrentDictionary<string, IReadOnlyDictionary<string, TypeTreeRepr>>(StringComparer.Ordinal);
        }

        public TpkTypeTreeBlob Blob { get; }

        /// <summary>Interned type trees, indexed the way the database nodes are.</summary>
        public TypeTreeRepr?[] Nodes { get; }

        /// <summary>Root type trees per Unity version, so a session does not walk the database twice.</summary>
        public ConcurrentDictionary<string, IReadOnlyDictionary<string, TypeTreeRepr>> RootsByVersion { get; }

        /// <summary>Open handles; guarded by <see cref="Gate"/>.</summary>
        public int Handles;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Database> Databases = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _path;
    private readonly Database _database;
    private bool _disposed;

    private TpkTypeTreeCatalog(string path, Database database)
    {
        _path = path;
        _database = database;
    }

    /// <summary>
    /// Opens the database at <paramref name="path"/>, reusing the one already open for that path. Every call has to
    /// be paired with a <see cref="Dispose"/>.
    /// </summary>
    public static TpkTypeTreeCatalog Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var fullPath = Path.GetFullPath(path);

        lock (Gate)
        {
            if (!Databases.TryGetValue(fullPath, out var database))
            {
                var dataBlob = TpkFile.FromFile(fullPath).GetDataBlob();
                if (dataBlob is not TpkTypeTreeBlob blob)
                {
                    throw new Exception(
                        $"Unsupported blob type: {dataBlob?.GetType().FullName ?? "null"}, expected TpkTypeTreeBlob.");
                }

                database = new Database(blob);
                Databases[fullPath] = database;
            }

            database.Handles++;
            return new TpkTypeTreeCatalog(fullPath, database);
        }
    }

    /// <summary>
    /// The root type trees of one Unity version, keyed by class name. Memoized per version string.
    /// </summary>
    public IReadOnlyDictionary<string, TypeTreeRepr> GetRootTypeNodes(string unityVersion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(unityVersion);

        return _database.RootsByVersion.GetOrAdd(unityVersion, BuildRootTypeNodes);
    }

    /// <summary>
    /// Releases this handle. The database is dropped once the last handle for that path is disposed; already interned
    /// type trees stay valid, because loaded files keep their own references to them.
    /// </summary>
    public void Dispose()
    {
        lock (Gate)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (--_database.Handles <= 0)
                Databases.Remove(_path);
        }

        GC.SuppressFinalize(this);
    }

    private IReadOnlyDictionary<string, TypeTreeRepr> BuildRootTypeNodes(string unityVersion)
    {
        var blob = _database.Blob;
        UnityVersion.TryParse(unityVersion, out var version, out _);

        var rootTypeNodesMap = new Dictionary<string, ushort>(StringComparer.Ordinal);

        foreach (var info in blob.ClassInformation)
        {
            var isSupportedVersion = false;

            // Classes are sorted by version.
            for (var i = 0; i < info.Classes.Count; i++)
            {
                var (_, unityClass) = info.Classes[i];
                if (!isSupportedVersion && i < info.Classes.Count - 1)
                {
                    var (nextVersion, _) = info.Classes[i + 1];
                    if (version < nextVersion)
                        isSupportedVersion = true;
                    else
                        continue;
                }

                if (unityClass is null)
                    continue;

                var name = blob.StringBuffer[unityClass.Name];
                if ((unityClass.Flags & TpkUnityClassFlags.HasReleaseRootNode) == 0)
                    continue;

                rootTypeNodesMap[name] = unityClass.ReleaseRootNode;

                if (isSupportedVersion)
                    break;
            }
        }

        return rootTypeNodesMap.ToDictionary(
            pair => pair.Key,
            pair => TpkNodeInterner.Create(_database.Blob, pair.Value, _database.Nodes),
            StringComparer.Ordinal);
    }
}