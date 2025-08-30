using UnityAsset.NET.Enums;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFile : IVirtualFile
{
    private readonly Stream _stream;
    
    public DirectFile(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        FileType = FileTypeHelper.GetFileType(this);
    }
    
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }
    public Stream Stream => _stream;
    public long Length => _stream.Length;
    public void Dispose()
    {
        _stream.Dispose();
    }
}