using UnityAsset.NET.Enums;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileSystem : IFileSystem
{
    public IFileSystem.ErrorHandler? OnError { get; set; }
    public List<IVirtualFileInfo> LoadedFiles { get; private set; } = new();

    public DirectFileSystem(IFileSystem.ErrorHandler? onError = null)
    {
        OnError = onError;
    }
    
    public Task<List<IVirtualFileInfo>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            var files = new List<IVirtualFileInfo>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    var file = new DirectFileInfo(path);
                    progress?.Report(new LoadProgress($"DirectFileSystem: Loading {file.Name}", totalFiles, i));
                    if (file.FileType != FileType.Unknown)
                        files.Add(file);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(path, ex, $"Fail to load DirectFile {path}: {ex.Message}");
                }
            }

            LoadedFiles = files;
            return LoadedFiles;
        });
    }
    
    public void Clear()
    {
        LoadedFiles = new();
    }
}