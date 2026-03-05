using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem
{
    public interface IVirtualFileInfo
    {
        public SafeFileHandle Handle { get; }
        public string Path { get; }
        public string Name { get; }
        public long Length { get; }
        public FileType FileType { get; }
        public IVirtualFile GetFile();
    }
}