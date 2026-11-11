namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileSystem : IFileSystem, IDisposable
{
    public IFileSystem.ErrorHandler? OnError { get; set; }
    public List<IVirtualFileInfo> LoadedFiles { get; private set; } = new ();

    public DirectFileSystem(IFileSystem.ErrorHandler? onError = null)
    {
        OnError = onError;
    }
    
    public Task<List<IVirtualFileInfo>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            // A load replaces the previous one, so what the last one opened is released here rather than at exit.
            ReleaseFiles();

            var files = new List<IVirtualFileInfo>();
            var totalFiles = paths.Count;
            for (int i = 0; i < totalFiles; i++)
            {
                var path = paths[i];
                try
                {
                    var file = new DirectFileInfo(path);
                    progress?.Report(new LoadProgress($"DirectFileSystem: Loading {file.Name}", totalFiles, i));
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
        ReleaseFiles();
        LoadedFiles = new();
    }

    public void Dispose() => Clear();

    public List<IVirtualFileInfo> LoadFile(List<string> paths)
    {
        var addedFiles = paths
            .Where(Path.Exists)
            .Select(p => (IVirtualFileInfo)new DirectFileInfo(p))
            .ToList();
        
        LoadedFiles.AddRange(addedFiles);
        return addedFiles;
    }

    /// <summary>
    /// Releases the handles of the files this instance opened. Only its own type is disposed: a file handed in from
    /// somewhere else owns whatever it wraps.
    /// </summary>
    private void ReleaseFiles()
    {
        foreach (var file in LoadedFiles)
        {
            if (file is DirectFileInfo direct)
                direct.Dispose();
        }
    }
}