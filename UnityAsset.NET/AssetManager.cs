using System.Collections.Concurrent;
using System.Diagnostics;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined.Types;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class AssetManager
{
    private IFileSystem _fileSystem;

    public readonly ConcurrentDictionary<string, IFile> LoadedFiles = new();
    
    public readonly ConcurrentDictionary<IVirtualFile, IFile> VirtualFileToFileMap = new();
    
    public UnityRevision? Version { get; private set; }
    public BuildTarget? BuildTarget { get; private set; }

    private List<SerializedType> _loadedTypes = new();
    
    public bool NeedTpk { get; private set; }
    
    public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null)
    {
        _fileSystem = fileSystem ?? new FileSystem.DirectFileSystem.DirectFileSystem(onError);
    }
    
    public void SetFileSystem(IFileSystem fileSystem)
    {
        Clear();
        _fileSystem = fileSystem;
    }

    public List<Asset> LoadedAssets => LoadedFiles.Values
        .OfType<SerializedFile>()
        .SelectMany(sf => sf.Assets)
        .ToList();

    public async Task LoadAsync(List<IVirtualFile> files, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            var fileWrappers = new ConcurrentBag<(string, IFile)>();
            var types = new ConcurrentBag<SerializedType>();
            int progressCount = 0;
            var total = files.Count;
            
            bool anyTypeTreeDisabled = false;

            Parallel.ForEach(files, file =>
            {
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file);
                        bundleFile.ParseFilesWithTypeConversion();
                        foreach (var fw in bundleFile.Files)
                        {
                            fileWrappers.Add((fw.Info.Path, fw.File));

                            if (fw is { File: SerializedFile sf })
                            {
                                if (!sf.Metadata.TypeTreeEnabled)
                                    Volatile.Write(ref anyTypeTreeDisabled, true);
                                foreach(var type in sf.Metadata.Types)
                                    types.Add(type);
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
                        
                        foreach(var type in serializedFile.Metadata.Types)
                            types.Add(type);

                        VirtualFileToFileMap[file] = serializedFile;
                        break;
                    }
                }
                int currentProgress = Interlocked.Increment(ref progressCount);
                progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", total, currentProgress));
            });
            
            foreach (var (path, file) in fileWrappers)
            {
                if (!LoadedFiles.TryAdd(path, file) && !ignoreDuplicatedFiles)
                {
                    throw new InvalidOperationException($"File {path} already loaded");
                }
            }

            var firstFile = LoadedFiles.Values
                .FirstOrDefault(file => file is SerializedFile);
            if (firstFile is SerializedFile firstSerializedFile)
            {
                BuildTarget = firstSerializedFile.Metadata.TargetPlatform;
                Version = firstSerializedFile.Metadata.UnityVersion;
            }

            _loadedTypes = new(types);

            if (anyTypeTreeDisabled)
            {
                NeedTpk = true;
                return;
            }

            BuildUnityTypes(progress);
        });
    }

    public void BuildUnityTypes(IProgress<LoadProgress>? progress = null)
    {
        if (TypeTreeCache.Cache.Count > 0)
        {
            progress?.Report(new LoadProgress($"AssetManager: Generating Types", 2, 1));
            AssemblyManager.LoadTypes(TypeTreeCache.ToTypeTreeHelperNodes().ToList());
            progress?.Report(new LoadProgress($"AssetManager: Generated {_loadedTypes.Count} types", 2, 2));
        }

        foreach (var (_, file) in LoadedFiles)
        {
            if (file is SerializedFile sf)
                sf.ProcessAssetBundle();
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
            if (file is IReader reader)
            {
                reader.Seek((long)streamingInfo.offset);
                return reader.ReadBytes((int)streamingInfo.size);
            }
        }

        return null;
    }

    public void Clear()
    {
        LoadedFiles.Clear();
        VirtualFileToFileMap.Clear();
        _fileSystem.Clear();
        TypeTreeCache.CleanCache();
    }

    public async Task LoadTpkFile(string path, string? version = null, IProgress<LoadProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            var tpkFile = TpkFile.FromFile(path);
            var blob = tpkFile.GetDataBlob();
        
            Debug.Assert(blob is TpkTypeTreeBlob);
            
            if (blob is TpkTypeTreeBlob tpkTypeTreeBlob)
            {
                TpkUnityTreeNodeFactory.Init(tpkTypeTreeBlob);
                var rootTypeNodesMap = TpkUnityTreeNodeFactory.GetRootTypeNodes(version ?? Version!.ToString());
                foreach (var loadedType in _loadedTypes)
                {
                    if (loadedType.Nodes.Count == 0)
                    {
                        var type = loadedType.ToTypeName();
                        Debug.Assert(rootTypeNodesMap.ContainsKey(type));
                        loadedType.Nodes = TypeTreeCache.GetOrAddNodes(loadedType.TypeHash, rootTypeNodesMap[type]);
                    }
                }
            
                BuildUnityTypes(progress);

                foreach (var (_, file) in LoadedFiles)
                {
                    if (file is SerializedFile {Metadata.TypeTreeEnabled : false} sf)
                    {
                        foreach (var asset in sf.Assets)
                            asset.UpdateTypeInfo();
                    }
                }
            }
            else
            {
                throw new Exception($"Unsupported blob type: {blob.GetType().FullName}, expected TpkTypeTreeBlob.");
            }
        });
    }
}