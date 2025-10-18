using System.Collections.Concurrent;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.TypeTreeHelper.PreDefined;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET;

public class AssetManager
{
    private IFileSystem _fileSystem;
    
    private readonly ConcurrentDictionary<string, IFile> _loadedFiles;
    
    public UnityRevision? Version { get; private set; }
    public BuildTarget? BuildTarget { get; private set; }
    
    public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null)
    {
        _fileSystem = fileSystem ?? new FileSystem.DirectFileSystem.DirectFileSystem(onError);
        _loadedFiles = new ();
    }
    
    public void SetFileSystem(IFileSystem fileSystem)
    {
        Clear();
        _fileSystem = fileSystem;
    }
    
    public List<IVirtualFile> LoadedFiles => _fileSystem.LoadedFiles;

    public List<Asset> LoadedAssets => _loadedFiles.Values
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

            Parallel.ForEach(files, file =>
            {
                int currentProgress = Interlocked.Increment(ref progressCount);
                progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", files.Count, currentProgress));
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file);
                        bundleFile.ParseFilesWithTypeConversion();
                        foreach (var fw in bundleFile.Files)
                        {
                            /*if (!_loadedFiles.TryAdd(fw.Info.Path, fw.File) && !ignoreDuplicatedFiles)
                            {
                                throw new InvalidOperationException($"File {fw.Info.Path} already loaded");
                            }*/
                            fileWrappers.Add((fw.Info.Path, fw.File));

                            if (fw is { File: SerializedFile sf })
                            {
                                foreach(var type in sf.Metadata.Types)
                                    types.Add(type);
                            }
                        }

                        break;
                    }
                    case FileType.SerializedFile:
                    {
                        var serializedFile = new SerializedFile(file);
                        /*if (!_loadedFiles.TryAdd(file.Name, serializedFile) && !ignoreDuplicatedFiles)
                        {
                            throw new InvalidOperationException($"File {file.Name} already loaded");
                        }*/
                        fileWrappers.Add((file.Name, serializedFile));

                        foreach(var type in serializedFile.Metadata.Types)
                            types.Add(type);
                        break;
                    }
                }
            });
            
            foreach (var (path, file) in fileWrappers)
            {
                if (!_loadedFiles.TryAdd(path, file) && !ignoreDuplicatedFiles)
                {
                    throw new InvalidOperationException($"File {path} already loaded");
                }
            }

            var firstFile = _loadedFiles.Values
                .FirstOrDefault(file => file is SerializedFile);
            if (firstFile is SerializedFile firstSerializedFile)
            {
                BuildTarget = firstSerializedFile.Metadata.TargetPlatform;
                Version = firstSerializedFile.Metadata.UnityVersion;
            }

            if (types.Count > 0)
            {
                progress?.Report(new LoadProgress($"AssetManager: Generating Types", 2, 1));
                var typeSet = types.DistinctBy(t => t.TypeHash).ToList();
                AssemblyManager.LoadTypes(typeSet);
                progress?.Report(new LoadProgress($"AssetManager: Generated {typeSet.Count} types", 2, 2));
            }
        });
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
        if (_loadedFiles.TryGetValue(path, out IFile? file))
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
        _loadedFiles.Clear();
        _fileSystem.Clear();
    }
}