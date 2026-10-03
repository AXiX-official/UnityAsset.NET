using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Types;
using UnityAsset.NET.Types.PreDefined.Types;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class AssetManager : IUnitySession, IDisposable
{
    private IFileSystem _fileSystem;

    public FrozenDictionary<string, IFile> LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();

    public ConcurrentDictionary<IVirtualFileInfo, IFile> VirtualFileToFileMap = new();

    public UnityRevision? Version { get; private set; }
    public BuildTarget? BuildTarget { get; private set; }

    public List<Asset> LoadedAssets { get; private set; } = new();

    private FrozenDictionary<Hash128, UnityTypeSource> _loadedTypes = FrozenDictionary<Hash128, UnityTypeSource>.Empty;
    private TypeRegistry _typeRegistry;
    private UnityObjectFactory? _factory;
    private TpkTypeTreeCatalog? _tpk;
    private readonly UnitySessionOptions _options;
    private readonly BlockCacheContext _blockCache;

    private int _disposed;

    private long InitialBlockCacheBudget => _options.BlockCacheSize ?? Setting.DefaultBlockCacheSize;

    /// <summary>1 while a mutating operation owns the session; see <see cref="EnterExclusive"/>.</summary>
    private int _busy;

    private bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) != 0;

    public IReadOnlyDictionary<Hash128, UnityTypeSource> LoadedTypes => _loadedTypes;

    /// <summary>
    /// The object factory for this session's catalog; null until types have been loaded (<see cref="LoadAsync"/>).
    /// It is created together with the catalog it materialises, and dropped again by <see cref="Clear"/>.
    /// </summary>
    public UnityObjectFactory? Factory => _factory;

    /// <summary>
    /// The decompressed block cache and its accounting, owned by this session. Exposed for instrumentation: the
    /// statistics, the entry count and the budget all live here.
    /// </summary>
    public BlockCacheContext BlockCache => _blockCache;

    public bool TryGetLoadedFile(string name, [NotNullWhen(true)] out IFile? file)
    {
        file = null;
        var fileFound = LoadedFiles.TryGetValue(name, out var sfw);
        if (fileFound)
        {
            file = sfw!;
            return true;
        }

        return false;
    }

    public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null,
                        UnitySessionOptions? options = null)
    {
        _fileSystem = fileSystem ?? new FileSystem.DirectFileSystem.DirectFileSystem(onError);
        _options = options ?? UnitySessionOptions.Default;
        _typeRegistry = new TypeRegistry(_options.Codegen);
        _blockCache = new BlockCacheContext(InitialBlockCacheBudget);
    }

    public void SetFileSystem(IFileSystem fileSystem)
    {
        Clear();
        _fileSystem = fileSystem;
    }

    public Task LoadAsync(List<IVirtualFileInfo> files, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        => RunExclusiveAsync(() => LoadCoreAsync(files, ignoreDuplicatedFiles, progress));

    private async Task LoadCoreAsync(List<IVirtualFileInfo> files, bool ignoreDuplicatedFiles, IProgress<LoadProgress>? progress)
    {
        await Task.Run(() =>
        {
            var fileWrappers = new ConcurrentBag<(string, IFile)>();
            int progressCount = 0;
            var total = files.Count;

            Parallel.ForEach(files, file =>
            {
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file, _options.UnityCnKey, _options.DefaultUnityVersion, _blockCache);
                        bundleFile.ParseFilesWithTypeConversion();
                        foreach (var fw in bundleFile.Files)
                        {
                            if (fw.File is SerializedFile parsedFile &&
                                parsedFile.ReaderProvider is SlicedReaderProvider { BaseReaderProvider: BlockReaderProvider blockProvider } slicedProvider)
                            {
                                RegisterAssetToBlockMap(slicedProvider, blockProvider, parsedFile);
                            }

                            fileWrappers.Add((fw.Info.Path, fw.File));
                        }

                        VirtualFileToFileMap[file] = bundleFile;
                        break;
                    }
                    case FileType.SerializedFile:
                    {
                        var serializedFile = new SerializedFile(file, _options.DefaultUnityVersion);
                        fileWrappers.Add((file.Name, serializedFile));
                        VirtualFileToFileMap[file] = serializedFile;
                        break;
                    }
                }
                int currentProgress = Interlocked.Increment(ref progressCount);
                progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", total, currentProgress));
            });

            _blockCache.RemoveBlocksReferencedByAtMost(2);

            if (_options.BlockCacheSize is null)
                _blockCache.Reset(_blockCache.TotalBlockSize * 3 / 4); // it works good for BuildSceneHierarchy

            var tmpLoadedFilesDict = new Dictionary<string, IFile>();

            foreach (var (path, file) in fileWrappers)
            {
                if (!tmpLoadedFilesDict.TryAdd(path, file) && !ignoreDuplicatedFiles)
                {
                    throw new InvalidOperationException($"File {path} already loaded");
                }
            }

            LoadedFiles = tmpLoadedFilesDict.ToFrozenDictionary();

            BuildUnityTypes(progress);

            LoadedAssets = LoadedFiles.Values
                .OfType<SerializedFile>()
                .SelectMany(sf => sf.Assets)
                .ToList();
        });
    }

    private void BuildUnityTypes(IProgress<LoadProgress>? progress = null)
    {
        progress?.Report(new LoadProgress($"AssetManager: Generating Types", 2, 1));

        var files = OrderedSerializedFiles().ToList();
        var declaring = files.FirstOrDefault(file => !string.IsNullOrEmpty(file.Metadata.UnityVersion));
        if (declaring is not null)
        {
            Version = declaring.Metadata.UnityVersion;
            BuildTarget = declaring.Metadata.TargetPlatform;
        }

        var catalog = CollectTypeCatalog(files, OpenTpkRoots);

        var factory = new UnityObjectFactory(catalog, _typeRegistry);

        _typeRegistry.LoadTypes(catalog);
        _loadedTypes = catalog.ToFrozenDictionary();
        _factory = factory;
        progress?.Report(new LoadProgress($"AssetManager: Generated {catalog.Count} types", 2, 2));

        var blockCache = _blockCache;
        var context = new AssetContext
        {
            // Method group: the context stores a delegate, not an abstraction, so the factory does not have to
            // implement anything to be handed over.
            Factory = factory.Create,
            OnParsed = asset => OnAssetParsed(blockCache, asset)
        };
        foreach (var serializedFile in files)
        {
            foreach (var asset in serializedFile.Assets)
            {
                asset.AttachContext(context);

                asset.IsNamedAsset = catalog.TryGetValue(asset.Info.Type.TypeHash, out var source) && source.TypeTree.IsNamed;
            }
        }

        // Second pass: AssetBundle processing materialises assets, and its preload table can point into other files,
        // so every file has to be attached before any of them is processed.
        foreach (var serializedFile in files)
        {
            serializedFile.ProcessAssetBundle();
        }
    }

    private IEnumerable<SerializedFile> OrderedSerializedFiles()
        => LoadedFiles
            .Where(pair => pair.Value is SerializedFile)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (SerializedFile)pair.Value);

    internal static Dictionary<Hash128, UnityTypeSource> CollectTypeCatalog(
        IEnumerable<SerializedFile> files,
        Func<string, IReadOnlyDictionary<string, TypeTreeRepr>> openTpkRoots)
    {
        var catalog = new Dictionary<Hash128, UnityTypeSource>();
        var tpkRootsByVersion = new Dictionary<string, IReadOnlyDictionary<string, TypeTreeRepr>>();
        var missingTypes = new List<string>();

        foreach (var file in files)
        {
            var unityVersion = file.Metadata.UnityVersion;
            var typeTreeEnabled = file.Metadata.TypeTreeEnabled;

            foreach (var type in file.Metadata.Types) //.Concat(file.Metadata.RefTypes ?? []))
            {
                if (catalog.ContainsKey(type.TypeHash))
                    continue;

                var typeName = ((AssetClassID)type.TypeID).ToString();

                if (typeTreeEnabled && type.Nodes.Length > 0)
                {
                    var repr = TypeTreeIntern.TryGet(type.TypeHash, out var entry) && entry.Repr is { } internedRepr
                        ? internedRepr
                        : type.Nodes[0].ToTypeTreeRepr(type.Nodes);
                    catalog[type.TypeHash] = new UnityTypeSource(typeName, unityVersion, repr);
                    continue;
                }

                if (!tpkRootsByVersion.TryGetValue(unityVersion, out var tpkRoots))
                {
                    tpkRoots = openTpkRoots(unityVersion);
                    tpkRootsByVersion[unityVersion] = tpkRoots;
                }

                if (tpkRoots.TryGetValue(typeName, out var tpkRepr))
                    catalog[type.TypeHash] = new UnityTypeSource(typeName, unityVersion, tpkRepr);
                else if (!missingTypes.Contains(typeName))
                    missingTypes.Add(typeName);
            }
        }

        if (missingTypes.Count > 0)
        {
            throw new NotSupportedException(
                "These types are neither carried by their own file nor present in the type tree database (tpk): " +
                string.Join(", ", missingTypes));
        }

        return catalog;
    }

    private IReadOnlyDictionary<string, TypeTreeRepr> OpenTpkRoots(string unityVersion)
    {
        _tpk ??= TpkTypeTreeCatalog.Open(_options.TpkFilePath ?? Setting.DefaultTpkFilePath);
        return _tpk.GetRootTypeNodes(unityVersion);
    }

    public Task LoadAsync(List<string> paths, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        => RunExclusiveAsync(async () =>
        {
            var virtualFiles = await _fileSystem.LoadAsync(paths, progress);

            await LoadCoreAsync(virtualFiles, ignoreDuplicatedFiles, progress);
        });

    public Task LoadDirectoryAsync(string directoryPath, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        => RunExclusiveAsync(async () =>
        {
            var filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            var virtualFiles = await _fileSystem.LoadAsync(filePaths.ToList(), progress);

            await LoadCoreAsync(virtualFiles, ignoreDuplicatedFiles, progress);
        });

    /// <summary>
    /// Runs a mutating operation under the session's exclusive flag.
    /// </summary>
    private async Task RunExclusiveAsync(Func<Task> operation)
    {
        EnterExclusive();
        try
        {
            await operation();
        }
        finally
        {
            ExitExclusive();
        }
    }

    private void EnterExclusive()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "This session is already running an operation, Use one session per parallel load instead.");
        }
    }

    private void ExitExclusive() => Volatile.Write(ref _busy, 0);

    public void Adopt(SerializedFile file, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        var effectiveName = name ?? (file.ParentBundle is null ? file.SourceVirtualFileInfo?.Name : null);
        if (string.IsNullOrEmpty(effectiveName))
        {
            throw new ArgumentException(
                "A file has to be adopted under a name.",
                nameof(name));
        }

        Adopt([(effectiveName, file)]);
    }

    public void Adopt(IEnumerable<(string Name, SerializedFile File)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        EnterExclusive();
        try
        {
            AdoptCore(files);
        }
        finally
        {
            ExitExclusive();
        }
    }

    private void AdoptCore(IEnumerable<(string Name, SerializedFile File)> files)
    {
        var adopted = files.ToList();
        if (adopted.Count == 0)
            return;

        // Validate first, so a rejected batch leaves the session exactly as it was.
        var directory = LoadedFiles.ToDictionary(pair => pair.Key, pair => pair.Value);
        var filesInSession = new HashSet<IFile>(LoadedFiles.Values);

        foreach (var (name, file) in adopted)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!filesInSession.Add(file))
                throw new InvalidOperationException("This file is already part of the session.");

            if (!directory.TryAdd(name, file))
                throw new InvalidOperationException($"File {name} is already loaded");
        }

        LoadedFiles = directory.ToFrozenDictionary();
        LoadedAssets.AddRange(adopted.SelectMany(entry => entry.File.Assets));

        BuildUnityTypes();
    }

    public byte[]? LoadStreamingData(StreamingInfo streamingInfo)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var path = streamingInfo.path.Split('/')[^1];
        if (LoadedFiles.TryGetValue(path, out IFile? file))
        {
            if (file is IReaderProvider readerProvider)
            {
                var reader = readerProvider.CreateReader();
                reader.Seek((long)streamingInfo.offset);
                return reader.ReadBytes((int)streamingInfo.size);
            }
        }

        return null;
    }

    internal static void OnAssetParsed(BlockCacheContext blockCache, Asset asset)
    {
        var sf = asset.SourceFile;
        if (sf.ReaderProvider is SlicedReaderProvider srp)
        {
            if (srp.BaseReaderProvider is BlockReaderProvider brp)
            {
                var offset = srp.Offset;
                var blocks = brp.Blocks;
                var pos = offset + sf.Header.DataOffset + asset.Info.ByteOffset;
                var index = BlockReader.FindBlockIndex(blocks, pos);
                // -1 means the asset starts outside every block, which a malformed or truncated file can produce.
                // Without this the loop below indexes blocks[-1] and reports an IndexOutOfRangeException that says
                // nothing about which asset or position was wrong.
                if (index < 0)
                    return;
                while (index < blocks.Length && pos + asset.Info.ByteSize > blocks[index].UncompressedOffset)
                {
                    var key = new BlockCacheKey(brp.File, index);
                    var size = blocks[index].UncompressedSize;
                    var newStats = blockCache.AssetToBlockCache.AddOrUpdate(key,
                        addValue: (-1, 1, 0),
                        updateValueFactory: (_, existing) =>
                            (existing.parsed + 1, existing.total, size));
                    if (newStats.parsed == newStats.total)
                    {
                        blockCache.Cache.Remove(key);
                    }

                    index++;
                }
            }
        }
    }

    internal void RegisterAssetToBlockMap(SlicedReaderProvider srp, BlockReaderProvider brp, SerializedFile sf)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var offset = srp.Offset;
        var blocks = brp.Blocks;
        foreach (var info in sf.Metadata.AssetInfos)
        {
            var pos = offset + sf.Header.DataOffset + info.ByteOffset;
            var index = BlockReader.FindBlockIndex(blocks, pos);
            // See OnAssetParsed: a -1 lookup has to be skipped, not dereferenced.
            if (index < 0)
                continue;
            while (index < blocks.Length && pos + info.ByteSize > blocks[index].UncompressedOffset)
            {
                var block = blocks[index];
                var key = new BlockCacheKey(brp.File, index);
                var size = block.UncompressedSize;
                index++;
                if (block.CompressionType == CompressionType.None)
                    continue;
                _blockCache.AssetToBlockCache.AddOrUpdate(key,
                    addValue: (0, 1, size),
                    updateValueFactory: (k, existing) =>
                        (existing.parsed, existing.total + 1, size));
            }
        }
    }

    public void Clear()
    {
        EnterExclusive();
        try
        {
            ReleaseSessionState();

            // The session stays usable, so it needs a registry to compile the next catalog into.
            _typeRegistry = new TypeRegistry(_options.Codegen);
        }
        finally
        {
            ExitExclusive();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ReleaseSessionState();
    }

    private void ReleaseSessionState()
    {
        foreach (var (_, file) in LoadedFiles)
        {
            if (file is not SerializedFile serializedFile)
                continue;

            foreach (var asset in serializedFile.Assets)
                asset.DetachContext();
        }

        LoadedAssets = new();
        _loadedTypes = FrozenDictionary<Hash128, UnityTypeSource>.Empty;
        _factory = null;
        VirtualFileToFileMap = new();
        LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();
        Version = null;
        BuildTarget = null;

        _fileSystem.Clear();

        _tpk?.Dispose();
        _tpk = null;
        _typeRegistry.Dispose();

        _blockCache.Reset(InitialBlockCacheBudget);
        _blockCache.ClearAccounting();
    }
}