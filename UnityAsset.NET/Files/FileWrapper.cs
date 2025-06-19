using UnityAsset.NET.Files.BundleFiles;

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

    // TODO: Better detect
    public bool CanBeSerializedFile => Info.Path.StartsWith("CAB-") && !Info.Path.EndsWith(".resS");
}