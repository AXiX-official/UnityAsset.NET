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

        /// <summary>
        /// Why the probe could not name this file, or null when it did. A source of files that looks at the bytes reports
        /// the probe's reason here, so a session can say what it saw instead of only that it saw nothing.
        /// </summary>
        public string? UnrecognisedReason => null;
    }
}