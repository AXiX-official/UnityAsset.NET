namespace UnityAsset.NET.FileSystem;

public interface IFileSystem
{
    public delegate void ErrorHandler(string filePath, Exception ex, string errorMessage);
    public ErrorHandler? OnError { get; set; }
    public List<IVirtualFile> LoadedFiles { get; }
    public Task<List<IVirtualFile>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null);
    public Task<List<IVirtualFile>> LoadDirectoryAsync(string directoryPath, IProgress<LoadProgress>? progress = null)
    {
        string[] filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        return LoadAsync(filePaths.ToList(), progress);
    }
    public void Clear();
}