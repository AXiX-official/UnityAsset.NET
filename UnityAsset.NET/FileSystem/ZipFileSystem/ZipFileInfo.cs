using Microsoft.Win32.SafeHandles;
using DirectFileCursor = UnityAsset.NET.FileSystem.DirectFileSystem.DirectFile;

namespace UnityAsset.NET.FileSystem.ZipFileSystem;

internal sealed class ZipFileInfo : IVirtualFileInfo, IDisposable
{
    private readonly byte[]? _data;
    private readonly SafeFileHandle? _handle;

    private ZipFileInfo(string archivePath, string entryPath, FileType fileType, byte[]? data, SafeFileHandle? handle,
        string? spillPath)
    {
        Path = archivePath + ":" + entryPath;
        Name = entryPath;
        FileType = fileType;
        _data = data;
        _handle = handle;
        SpillPath = spillPath;
        Length = data?.LongLength ?? RandomAccess.GetLength(handle!);
    }

    /// <summary>Small enough to keep: the entry was inflated into a buffer.</summary>
    public static ZipFileInfo InMemory(string archivePath, string entryPath, byte[] data, FileType fileType)
        => new(archivePath, entryPath, fileType, data, null, null);

    /// <summary>Too large for a buffer: the entry was inflated into <paramref name="spillPath"/>.</summary>
    public static ZipFileInfo OnDisk(string archivePath, string entryPath, string spillPath, FileType fileType)
        => new(archivePath, entryPath, fileType, null,
            File.OpenHandle(spillPath, FileMode.Open, FileAccess.Read, FileShare.Read), spillPath);

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);

    public string Path { get; }

    public string Name { get; }

    public long Length { get; }

    public FileType FileType { get; }

    /// <summary>
    /// Where the bytes were spilled, or null when they are in memory. The file system that created it deletes it; this
    /// type only closes the read handle it owns.
    /// </summary>
    internal string? SpillPath { get; }

    /// <summary>
    /// A fresh cursor. A spilled entry shares one read handle the way a file on disk does, so a cursor is a position and
    /// nothing else; an in-memory entry copies nothing either, it points at the same buffer.
    /// </summary>
    public IVirtualFile GetFile()
        => _data is { } data ? new MemoryFile(data) : new DirectFileCursor(_handle!, 0, Length);

    /// <summary>
    /// Closes the read handle. Deleting the spilled file is the file system's job, because it is the one holding the error
    /// channel a failed delete has to be reported through.
    /// </summary>
    public void Dispose() => _handle?.Dispose();

    /// <summary>A cursor over the inflated bytes: a position into a buffer nobody else can move.</summary>
    private sealed class MemoryFile(byte[] bytes) : IVirtualFile
    {
        public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);

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
