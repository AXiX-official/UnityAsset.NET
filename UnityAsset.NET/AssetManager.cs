using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined.Types;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class AssetManager
{
    private IFileSystem _fileSystem;

    public FrozenDictionary<string, IFile> LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();
    
    public ConcurrentDictionary<IVirtualFileInfo, IFile> VirtualFileToFileMap = new();
    
    public UnityRevision? Version { get; private set; }
    public BuildTarget? BuildTarget { get; private set; }

    public List<Asset> LoadedAssets { get; private set; } = new();
    
    public static Dictionary<Hash128, TypeTreeRepr> LoadedTypes = new();
    
    public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null)
    {
        _fileSystem = fileSystem ?? new FileSystem.DirectFileSystem.DirectFileSystem(onError);
    }
    
    public void SetFileSystem(IFileSystem fileSystem)
    {
        Clear();
        _fileSystem = fileSystem;
    }

    public async Task LoadAsync(List<IVirtualFileInfo> files, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            var fileWrappers = new ConcurrentBag<(string, IFile)>();
            int progressCount = 0;
            var total = files.Count;
            
            bool anyTypeTreeDisabled = false;

            Parallel.ForEach(files, file =>
            {
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file, lazyLoad: false);
                        foreach (var fw in bundleFile.Files)
                        {
                            fileWrappers.Add((fw.Info.Path, fw.File));

                            if (fw is { File: SerializedFile sf })
                            {
                                if (!sf.Metadata.TypeTreeEnabled && !anyTypeTreeDisabled)
                                    Volatile.Write(ref anyTypeTreeDisabled, true);
                            }
                        }

                        VirtualFileToFileMap[file] = bundleFile;
                        break;
                    }
                    case FileType.SerializedFile:
                    {
                        var serializedFile = new SerializedFile(file);
                        fileWrappers.Add((file.Name, serializedFile));

                        if (!serializedFile.Metadata.TypeTreeEnabled)
                            Volatile.Write(ref anyTypeTreeDisabled, true);

                        VirtualFileToFileMap[file] = serializedFile;
                        break;
                    }
                }
                int currentProgress = Interlocked.Increment(ref progressCount);
                progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", total, currentProgress));
            });
            
            BlockReader.RemoveSingleReferenceBlocks();
            BlockReader.Cache = new(maxSize: BlockReader.TotalBlockSize * 3 / 4); // it works good for BuildSceneHierarchy
            
            var tmpLoadedFilesDict = new Dictionary<string, IFile>();
            
            foreach (var (path, file) in fileWrappers)
            {
                if (!tmpLoadedFilesDict.TryAdd(path, file) && !ignoreDuplicatedFiles)
                {
                    throw new InvalidOperationException($"File {path} already loaded");
                }
            }

            LoadedFiles = tmpLoadedFilesDict.ToFrozenDictionary();

            var firstFile = LoadedFiles.Values
                .FirstOrDefault(file => file is SerializedFile);
            if (firstFile is SerializedFile firstSerializedFile)
            {
                BuildTarget = firstSerializedFile.Metadata.TargetPlatform;
                Version = firstSerializedFile.Metadata.UnityVersion;
            }

            BuildUnityTypes(anyTypeTreeDisabled, progress);
            
            LoadedAssets = LoadedFiles.Values
                .OfType<SerializedFile>()
                .SelectMany(sf => sf.Assets)
                .ToList();
        });
    }

    private void BuildUnityTypes(bool anyTypeTreeDisabled, IProgress<LoadProgress>? progress = null)
    {
        progress?.Report(new LoadProgress($"AssetManager: Generating Types", 2, 1));

        if (anyTypeTreeDisabled)
        {
            var tpkFile = TpkFile.FromFile(Setting.DefaultTpkFilePath);
            var blob = tpkFile.GetDataBlob();
        
            Debug.Assert(blob is TpkTypeTreeBlob);
            
            if (blob is TpkTypeTreeBlob tpkTypeTreeBlob)
            {
                TpkUnityTreeNodeFactory.Init(tpkTypeTreeBlob);
            }
            else
            {
                throw new Exception($"Unsupported blob type: {blob.GetType().FullName}, expected TpkTypeTreeBlob.");
            }
        }
        
        var rootTypeNodesMap = anyTypeTreeDisabled ? TpkUnityTreeNodeFactory.GetRootTypeNodes(Version!.ToString()) : null;
        
        foreach (var (hash, (nodes, typeId, repr)) in TypeTreeNode.Cache)
        {
            if (nodes.Length == 0 && anyTypeTreeDisabled)
            {
                var typeName = ((AssetClassID)typeId).ToString();
                LoadedTypes.Add(hash, rootTypeNodesMap![typeName]);
            }
            else
            {
                LoadedTypes.Add(hash, repr ?? nodes[0].ToTypeTreeRepr(nodes));
            }
        }
        
        AssemblyManager.LoadTypes(LoadedTypes);
        progress?.Report(new LoadProgress($"AssetManager: Generated {TypeTreeNode.Cache.Count} types", 2, 2));

        foreach (var (_, file) in LoadedFiles)
        {
            if (file is SerializedFile sf)
                sf.ProcessAssetBundle();
        }

        if (anyTypeTreeDisabled)
        {
            foreach (var (_, file) in LoadedFiles)
            {
                if (file is SerializedFile {Metadata.TypeTreeEnabled : false} sf)
                {
                    foreach (var asset in sf.Assets)
                    {
                        if (asset.IsNamedAsset) continue;
                        var typeTreeRepr = LoadedTypes[asset.Info.Type.TypeHash];
                        foreach (var field in typeTreeRepr.SubNodes)
                        {
                            if (field.Name == "m_Name")
                            {
                                asset.IsNamedAsset = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
    
    public async Task LoadAsync(List<string> paths, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
    {
        var virtualFiles = await _fileSystem.LoadAsync(paths, progress);
        
        await LoadAsync(virtualFiles, ignoreDuplicatedFiles, progress);
    }

    public async Task LoadDirectoryAsync(string directoryPath, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
    {
        string[] filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        await LoadAsync(filePaths.ToList(), ignoreDuplicatedFiles, progress);
    }

    public byte[]? LoadStreamingData(StreamingInfo streamingInfo)
    {
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

    public void Clear()
    {
        LoadedAssets = new();
        LoadedTypes = new();
        VirtualFileToFileMap = new();
        LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();
        Version = null;
        BuildTarget = null;
        
        _fileSystem.Clear();
        
        TypeTreeNode.Cache = new();
        TypeTreeRepr.Cache = new();
        TpkUnityTreeNodeFactory.Deinit();
        AssemblyManager.CleanCache();
        BlockReader.Cache.Reset(Setting.DefaultBlockCacheSize);
        BlockReader.AssetToBlockCache = new();
    }
}