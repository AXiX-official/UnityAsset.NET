using UnityAsset.NET.Enums;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFile : IVirtualFile
{
    public DirectFile(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        using var stream = OpenStream();
        Length = stream.Length;
        FileType = FileTypeHelper.GetFileType(stream);
    }

    public static DirectFile Create(string path)
    {
        return new DirectFile(path);
    }
    
    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType { get; }
    public Stream OpenStream() => new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
}