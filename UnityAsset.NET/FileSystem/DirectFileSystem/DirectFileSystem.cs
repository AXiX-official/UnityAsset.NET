using System.Collections.Concurrent;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileSystem : IFileSystem
{
    public IFileSystem.ErrorHandler? OnError { get; set; }
    private readonly ConcurrentDictionary<string, IVirtualFile> _loadedFiles = new();

    public DirectFileSystem(IFileSystem.ErrorHandler? onError)
    {
        OnError = onError;
    }
    
    public List<IVirtualFile> LoadedFiles => _loadedFiles.Values.ToList();

    public Task<List<IVirtualFile>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var files = new List<IVirtualFile>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    var file = DirectFile.Create(path);
                    progress?.Report(new LoadProgress($"DirectFileSystem: Loading {file.Name}", totalFiles, i));
                    if (file.FileType == FileType.Unknown)
                    {
                        continue;
                    }
                    else
                    {
                        if (_loadedFiles.TryAdd(file.Name, file))
                        {
                            files.Add(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(path, ex, $"Fail to load DirectFile {path}: {ex.Message}");
                }
            }

            return files;
        });
    }
    
    public void Clear()
    {
        _loadedFiles.Clear();
    }
    
    public void Dispose()
    {
        Clear();
    }
}