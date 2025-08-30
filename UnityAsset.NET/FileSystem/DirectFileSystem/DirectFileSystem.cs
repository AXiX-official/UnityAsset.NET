using System.Collections.Concurrent;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, IVirtualFile> _loadedFiles = new();

    public DirectFileSystem()
    {

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
                var file = new DirectFile(path);
                progress?.Report(new LoadProgress($"DirectFileSystem: Loading {file.Name}", totalFiles, i));
                if (file.FileType == FileType.Unknown)
                {
                    file.Dispose();
                }
                else
                {
                    if (_loadedFiles.TryAdd(file.Name, file))
                    {
                        files.Add(file);
                    }
                    else
                    {
                        file.Dispose();
                    }
                }
            }

            return files;
        });
    }
    
    public void Clear()
    {
        foreach (var file in _loadedFiles.Values)
        {
           file.Dispose(); 
        }
        _loadedFiles.Clear();
    }
    
    public void Dispose()
    {
        Clear();
    }
}