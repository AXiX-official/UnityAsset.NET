using UnityAsset.NET.Enums;
using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileInfo : IVirtualFileInfo
{
    public SafeFileHandle Handle { get; }
    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType { get; }
    
    public DirectFileInfo(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Length = new FileInfo(path).Length;
        Handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        FileType = FileTypeHelper.GetFileType(this);
    }
    
    public IVirtualFile GetFile() => new DirectFile(Handle, 0, Length);
}