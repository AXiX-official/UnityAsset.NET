using UnityAsset.NET.Enums;
using UnityAsset.NET.IO.Stream;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFile : IVirtualFile
{
    private readonly FileStreamProvider _streamProvider;
    
    public DirectFile(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        _streamProvider = new FileStreamProvider(path);
        FileType = FileTypeHelper.GetFileType(_streamProvider);
    }
    
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }
    public Stream OpenStream() => _streamProvider.OpenStream();
}