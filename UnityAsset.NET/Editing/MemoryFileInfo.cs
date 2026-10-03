using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.Editing;

internal sealed class MemoryFileInfo(string path, byte[] data, FileType fileType) : IVirtualFileInfo
{
    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, false);
    public string Path => path;
    public string Name => System.IO.Path.GetFileName(path);
    public long Length => data.Length;
    public FileType FileType => fileType;
    public IVirtualFile GetFile() => new MemoryFile(data);

    private sealed class MemoryFile(byte[] bytes) : IVirtualFile
    {
        public SafeFileHandle Handle { get; } = new(IntPtr.Zero, false);
        public long Length => bytes.Length;
        public long Position { get; set; }
        public uint Read(Span<byte> buffer, uint offset, uint count)
        {
            var length = checked((int)Math.Min(count, Math.Max(0, Length - Position)));
            bytes.AsSpan(checked((int)Position), length).CopyTo(buffer.Slice(checked((int)offset), length));
            Position += length;
            return (uint)length;
        }
        public IVirtualFile Clone() => new MemoryFile(bytes) { Position = Position };
    }
}
