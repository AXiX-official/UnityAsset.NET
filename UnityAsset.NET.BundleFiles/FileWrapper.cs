using UnityAsset.NET.Files;

namespace UnityAsset.NET.BundleFiles
{
    public class FileWrapper
    {
        public IFile File { get; set; }
        public FileEntry Info { get; }
    
    
        public FileWrapper(IFile file, FileEntry info)
        {
            File = file;
            Info = info;
        }
    
        public bool CanBeSerializedFile => (Info.Flags & 0x04) != 0;
    }
}