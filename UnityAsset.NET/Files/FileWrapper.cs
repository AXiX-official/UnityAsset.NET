using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files;

public class FileWrapper
{
    public readonly IReaderProvider? FileProvider;
    private readonly IFile? _file;
    public IFile File => _file ?? FileProvider!.CreateReader();
    public FileEntry Info { get; }
    public bool Parsed { get; private set; }
    
    public FileWrapper(IReaderProvider fileProvider, FileEntry info)
    {
        FileProvider = fileProvider;
        Info = info;
        Parsed = !CanBeSerializedFile;
    }
    
    public FileWrapper(IFile file, FileEntry info)
    {
        _file = file;
        Info = info;
        Parsed = !CanBeSerializedFile;
    }
    
    public bool CanBeSerializedFile => (Info.Flags & 0x04) != 0;
}