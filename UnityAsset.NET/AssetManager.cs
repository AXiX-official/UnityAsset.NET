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
    
    public BuildTarget? BuildTarget { get; private set; }

    public AssetManager(IFileSystem? fileSystem)
    {
        _fileSystem = fileSystem ?? new FileSystem.DirectFileSystem.DirectFileSystem();
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

    public async Task LoadAsync(List<IVirtualFile> files, IProgress<LoadProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            List<SerializedType> types = new();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];

                progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", files.Count, i));
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file);
                        bundleFile.ParseFilesWithTypeConversion();
                        foreach (var fw in bundleFile.Files)
                        {
                            if (!_loadedFiles.TryAdd(fw.Info.Path, fw.File))
                            {
                                throw new InvalidOperationException($"File {fw.Info.Path} already loaded");
                            }

                            if (fw is { File: SerializedFile sf })
                            {
                                types.AddRange(sf.Metadata.Types);
                            }
                        }

                        break;
                    }
                    case FileType.SerializedFile:
                    {
                        var serializedFile = new SerializedFile(file);
                        if (!_loadedFiles.TryAdd(file.Name, serializedFile))
                        {
                            throw new InvalidOperationException($"File {file.Name} already loaded");
                        }

                        types.AddRange(serializedFile.Metadata.Types);
                        break;
                    }
                }
            }
            
            if (BuildTarget == null)
            {
                var file = _loadedFiles.Values
                    .FirstOrDefault(file => file is SerializedFile);
                if (file is SerializedFile sf)
                {
                    BuildTarget = sf.Metadata.TargetPlatform;
                }
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
    
    public async Task LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        var virtualFiles = await _fileSystem.LoadAsync(paths, progress);
        
        await LoadAsync(virtualFiles, progress);
    }

    public async Task LoadDirectoryAsync(string directoryPath, IProgress<LoadProgress>? progress = null)
    {
        string[] filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        await LoadAsync(filePaths.ToList(), progress);
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