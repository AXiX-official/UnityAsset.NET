namespace UnityAsset.NET.FileSystem;

public interface IVirtualFile
{
    public string Path { get; }
    public string FileName { get; }
    public Stream Stream { get; }
}