using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.Diagnostics;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.FileSystem.ZipFileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Types;
using UnityAsset.NET.Types.PreDefined.Types;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class AssetManager : IUnitySession, IDisposable
{
    private IFileSystem _fileSystem;

    public Dictionary<string, IFile> LoadedFiles = new ();

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

    /// <summary>What this session could not read, when its policy allowed skipping; empty otherwise.</summary>
    private readonly LoadDiagnosticBag _diagnostics = new();

    /// <summary>
    /// Types the session knows it cannot read, mapped to the reason. Written while types are built, read afterwards, and
    /// read again when an asset of one of them is materialised — which is why it is a dictionary of reasons rather than a
    /// set of hashes.
    /// </summary>
    private readonly Dictionary<Hash128, string> _unreadableTypes = new();

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

    /// <summary>
    /// Everything this session could not read. Empty while every stage is on <see cref="ErrorPolicy.Throw"/>, and one
    /// entry per skipped item once a stage is on <see cref="ErrorPolicy.SkipAndReport"/>; each entry says which
    /// <see cref="LoadProblem"/> it was.
    /// </summary>
    public IReadOnlyList<LoadDiagnostic> Diagnostics => _diagnostics.Diagnostics;

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
        _fileSystem = fileSystem ?? new DirectFileSystem(onError);
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
            var fileWrappers = new ConcurrentBag<(string Path, IFile File)>();
            var total = files.Count;

            _diagnostics.Clear();
            _unreadableTypes.Clear();
            
            var toLoadFilesHash = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                toLoadFilesHash.TryAdd(f.Name, 0);
            var nonexistentFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            
            var workQueue = new ConcurrentQueue<IVirtualFileInfo>(files);
            var pending = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var f in files) pending[f.Path] = 0;
            int progressCount = 0;
            int inFlight = 0;
            
            void EnqueueIfNew(string path, IVirtualFileInfo info)
            {
                if (pending.TryAdd(path, 0))
                    workQueue.Enqueue(info);
            }
            
            var workers = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => Task.Run(() =>
            {
                while (true)
                {
                    if (!workQueue.TryDequeue(out var file))
                    {
                        if (Volatile.Read(ref inFlight) == 0 && workQueue.IsEmpty)
                            return;
                        Thread.Sleep(1);
                        continue;
                    }

                    Interlocked.Increment(ref inFlight);
                    try
                    {
                        ProcessOne(file);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref inFlight);
                    }

                    int current = Interlocked.Increment(ref progressCount);
                    progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", total, current));
                }
            })).ToArray();

            void ProcessOne(IVirtualFileInfo file)
            {
                try
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
                            foreach (var sharedFile in serializedFile.Metadata.Externals)
                            {
                                var sharedFileName = sharedFile.FileName;
                                if (!toLoadFilesHash.TryAdd(sharedFileName, 0)) continue;
                                var sharedFilePath = Path.Combine(Path.GetDirectoryName(file.Path)!, sharedFileName);
                                if (nonexistentFiles.ContainsKey(sharedFilePath)) continue;
                                if (!File.Exists(sharedFilePath))
                                {
                                    var findFiles = Directory.GetFiles(Path.GetDirectoryName(file.Path)!, sharedFileName, SearchOption.AllDirectories);
                                    if (findFiles.Length == 0)
                                    {
                                        nonexistentFiles.TryAdd(sharedFilePath, 0);
                                        continue;
                                    }
                                    sharedFilePath = findFiles[0];
                                }

                                var loaded = _fileSystem.LoadFile([sharedFilePath]);
                                foreach (var path in loaded)
                                {
                                    EnqueueIfNew(path.Path, path);
                                    toLoadFilesHash.TryAdd(path.Path, 0);
                                }
                            }
                            fileWrappers.Add((file.Name, serializedFile));
                            VirtualFileToFileMap[file] = serializedFile;
                            break;
                        }
                        default:
                            // An input the sniffer could not place. Dropping it silently used to be the only option, which
                            // made "this file was not read" indistinguishable from "this file held nothing".
                            _diagnostics.Report(LoadProblem.UnknownFile,
                                "the input was not recognised as a serialized file or a bundle",
                                new LoadLocation(file.Path));
                            break;
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException &&
                                                  _options.CorruptFile == ErrorPolicy.SkipAndReport)
                {
                    // One unreadable container must not take the session down with it. Out of memory is excluded: it is
                    // not evidence about this file, and continuing after it is not something to promise.
                    _diagnostics.Report(LoadProblem.FileUnreadable,
                        $"{exception.GetType().Name}: {exception.Message}",
                        new LoadLocation(file.Path));
                }
            }

            Task.WaitAll(workers);

            _blockCache.RemoveBlocksReferencedByAtMost(2);

            if (_options.BlockCacheSize is null)
                _blockCache.Reset(_blockCache.TotalBlockSize * 3 / 4); // it works good for BuildSceneHierarchy

            LoadedFiles = new Dictionary<string, IFile>();

            // Sorted before insertion, so which copy of a duplicated node name survives does not depend on the order a
            // ConcurrentBag happens to enumerate — the winner used to change between runs.
            foreach (var (path, file) in fileWrappers.OrderBy(wrapper => wrapper.Path, StringComparer.Ordinal))
            {
                if (LoadedFiles.TryAdd(path, file))
                    continue;

                if (!ignoreDuplicatedFiles)
                    throw new InvalidOperationException($"File {path} already loaded");

                _diagnostics.Report(LoadProblem.DuplicateFile,
                    "a file of this name was already loaded, and this copy was dropped",
                    new LoadLocation(path));
            }
            
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

        var catalog = CollectTypeCatalog(files, OpenTpkRoots, _options.MissingType, _diagnostics, _unreadableTypes);

        var factory = new UnityObjectFactory(catalog, _typeRegistry);

        _typeRegistry.LoadTypes(catalog, _options.MissingType, out var skipped);
        foreach (var skippedHash in skipped)
        {
            // Already reported as missing, or not in this catalog at all: the reason the session keeps is the one the
            // earlier stage recorded, because that is the one that decides what the asset can be used for.
            if (!catalog.TryGetValue(skippedHash, out var source) || _unreadableTypes.ContainsKey(skippedHash))
                continue;

            var reason = $"the generated assembly has no type for {source.ClassName}";
            _unreadableTypes[skippedHash] = reason;
            _diagnostics.Report(LoadProblem.TypeNotGenerated,
                "the type tree was readable but no type was generated for it",
                new LoadLocation(TypeName: source.ClassName));
        }

        _loadedTypes = catalog.ToFrozenDictionary();
        _factory = factory;
        progress?.Report(new LoadProgress(
            $"AssetManager: Generated {catalog.Count} types" +
            (_unreadableTypes.Count > 0 ? $", {_unreadableTypes.Count} unreadable" : string.Empty), 2, 2));

        var blockCache = _blockCache;
        var context = new AssetContext
        {
            // Method group: the context stores a delegate, not an abstraction, so the factory does not have to
            // implement anything to be handed over.
            Factory = factory.Create,
            OnParsed = asset => OnAssetParsed(blockCache, asset),
            OnFailed = OnAssetFailed
        };
        foreach (var serializedFile in files)
        {
            foreach (var asset in serializedFile.Assets)
            {
                asset.AttachContext(context);

                asset.IsNamedAsset = catalog.TryGetValue(asset.Info.Type.TypeHash, out var source) && source.TypeTree.IsNamed;

                // A type the session already knows it cannot read marks all of its assets up front: one diagnostic per
                // type instead of one per asset, and `Asset.Name` can then report the truth instead of an empty name.
                if (_unreadableTypes.TryGetValue(asset.Info.Type.TypeHash, out var reason))
                    asset.MarkUnavailable(new AssetUnavailableException(asset.TypeName, asset.PathId, reason));
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
        Func<string, IReadOnlyDictionary<string, TypeTreeRepr>> openTpkRoots,
        ErrorPolicy missingType,
        LoadDiagnosticBag diagnostics,
        IDictionary<Hash128, string> unreadable)
    {
        var catalog = new Dictionary<Hash128, UnityTypeSource>();
        var tpkRootsByVersion = new Dictionary<string, IReadOnlyDictionary<string, TypeTreeRepr>>();
        var missingTypes = new List<(string ClassName, Hash128 Hash)>();

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
                else if (!missingTypes.Exists(missing => missing.ClassName == typeName))
                    missingTypes.Add((typeName, type.TypeHash));
            }
        }

        if (missingTypes.Count > 0)
        {
            if (missingType == ErrorPolicy.Throw)
            {
                throw new NotSupportedException(
                    "These types are neither carried by their own file nor present in the type tree database (tpk): " +
                    string.Join(", ", missingTypes.Select(missing => missing.ClassName)));
            }

            // Skip: the type is unreadable, not absent. Every asset of it is marked unavailable where the context is
            // attached, and one diagnostic per type says why.
            foreach (var (className, hash) in missingTypes)
            {
                var reason = $"{className}: the type tree is neither in the file nor in the tpk database";
                unreadable[hash] = reason;
                diagnostics.Report(LoadProblem.MissingType,
                    "the type tree is neither in the file nor in the tpk database",
                    new LoadLocation(TypeName: className));
            }
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

        var filesInSession = new HashSet<IFile>(LoadedFiles.Values);

        foreach (var (name, file) in adopted)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (!filesInSession.Add(file))
                throw new InvalidOperationException("This file is already part of the session.");

            if (!LoadedFiles.TryAdd(name, file))
                throw new InvalidOperationException($"File {name} is already loaded");
        }

        LoadedAssets.AddRange(adopted.SelectMany(entry => entry.File.Assets));

        BuildUnityTypes();
    }

    private void LoadResourceFile(List<string> paths)
    {
        var loadedFiles = _fileSystem.LoadFile(paths);
        foreach (var file in loadedFiles)
        {
            LoadedFiles.TryAdd(file.Name, new CustomFileReaderProvider(file));
        }
    }

    public byte[]? LoadStreamingData(StreamingInfo streamingInfo, IVirtualFileInfo? src)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var path = streamingInfo.path.Split('/')[^1];

        if (!LoadedFiles.ContainsKey(path) && src is not null)
        {
            var srcPath = src.Path;
            if (srcPath.Contains(ZipFileSystem.ArchiveSeparator))
            {
                LoadResourceFile([srcPath.Split(ZipFileSystem.ArchiveSeparator)[0] + ZipFileSystem.ArchiveSeparator + path]);
            }
            else
            {
                var dir = Path.GetDirectoryName(srcPath);
                if (dir is not null)
                {
                    var fullPath = Path.Combine(dir, path);
                    LoadResourceFile([fullPath]);
                }
            }
        }
        
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

    /// <summary>
    /// Decides what a failed materialisation does. Null means "rethrow the original", which is what
    /// <see cref="ErrorPolicy.Throw"/> says and what every caller gets by default; an exception means "record it and mark
    /// this asset unavailable".
    /// <para>
    /// A type the session already knows it cannot read uses the <see cref="UnitySessionOptions.MissingType"/> policy even
    /// when <see cref="UnitySessionOptions.CorruptAsset"/> stays strict, because those two questions have different
    /// answers: an unknown type is a gap in the tool's knowledge, a corrupt object is a damaged file.
    /// </para>
    /// </summary>
    private AssetUnavailableException? OnAssetFailed(Asset asset, Exception exception)
    {
        var typeIsUnreadable = _unreadableTypes.ContainsKey(asset.Info.Type.TypeHash);
        var missingType = typeIsUnreadable || exception is NotSupportedException;

        if ((missingType ? _options.MissingType : _options.CorruptAsset) == ErrorPolicy.Throw)
            return null;

        _diagnostics.Report(
            missingType ? LoadProblem.MissingType : LoadProblem.AssetUnreadable,
            exception.Message,
            new LoadLocation(asset.SourceFile.SourceVirtualFileInfo?.Name, asset.Container, asset.TypeName, asset.PathId));

        return new AssetUnavailableException(asset.TypeName, asset.PathId, exception.Message, exception);
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
        _diagnostics.Clear();
        _unreadableTypes.Clear();
        VirtualFileToFileMap = new();
        LoadedFiles = new ();
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