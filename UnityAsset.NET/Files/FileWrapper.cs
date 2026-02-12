using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files;

public class FileWrapper
{
    public IFile File { get; }
    public FileEntry Info { get; }
    
    
    public FileWrapper(IFile file, FileEntry info)
    {
        File = file;
        Info = info;
    }
    
    public bool CanBeSerializedFile => (Info.Flags & 0x04) != 0;
}