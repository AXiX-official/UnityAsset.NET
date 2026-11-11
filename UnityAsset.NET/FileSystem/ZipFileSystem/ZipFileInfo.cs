using Microsoft.Win32.SafeHandles;
using DirectFileCursor = UnityAsset.NET.FileSystem.DirectFileSystem.DirectFile;

namespace UnityAsset.NET.FileSystem.ZipFileSystem;

internal sealed class ZipFileInfo : IVirtualFileInfo, IDisposable
{
    private readonly Lazy<ZipEntryStorage> _storage;

    public ZipFileInfo(string archivePath, string entryPath, long length, FileType fileType,
        Func<ZipFileInfo, ZipEntryStorage> materialise)
    {
        ArchivePath = archivePath;
        EntryPath = entryPath;
        
        Path = archivePath + ZipFileSystem.ArchiveSeparator + entryPath;
        Name = entryPath;
        Length = length;
        FileType = fileType;

        _storage = new Lazy<ZipEntryStorage>(() => materialise(this), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Where the entry comes from: the archive has to be reopened when the bytes are finally wanted.</summary>
    internal string ArchivePath { get; }

    /// <summary>The entry's name inside the archive, as the central directory spells it.</summary>
    internal string EntryPath { get; }

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);

    public string Path { get; }

    public string Name { get; }

    public long Length { get; }

    public FileType FileType { get; }

    /// <summary>Whether the payload has been read yet. An entry nobody opened holds nothing and leaks nothing.</summary>
    internal bool IsMaterialised => _storage.IsValueCreated;

    /// <summary>Where the bytes were spilled, or null while the entry is unopened or held in a buffer.</summary>
    internal string? SpillPath => IsMaterialised ? _storage.Value.SpillPath : null;

    /// <summary>A fresh cursor over the payload, materialising it on the first call.</summary>
    public IVirtualFile GetFile() => _storage.Value.File;

    /// <summary>Releases the payload if there is one. Deleting a spilled file is the file system's job.</summary>
    public void Dispose()
    {
        if (_storage.IsValueCreated)
            _storage.Value.Dispose();
    }
}

/// <summary>
/// The payload of one entry: a buffer, or a file with the read handle every cursor shares. Which one it is was decided by
/// the entry's declared length before anything was read, and both answer the same random-access cursor because the
/// readers seek.
/// </summary>
internal sealed class ZipEntryStorage : IDisposable
{
    private readonly byte[]? _data;
    private readonly SafeFileHandle? _handle;

    private ZipEntryStorage(byte[]? data, SafeFileHandle? handle, long length, string? spillPath)
    {
        _data = data;
        _handle = handle;
        Length = length;
        SpillPath = spillPath;
    }

    /// <summary>Small enough to keep: the entry was inflated into a buffer.</summary>
    public static ZipEntryStorage InMemory(byte[] data) => new(data, null, data.LongLength, null);

    /// <summary>Too large for a buffer: the entry was inflated into <paramref name="spillPath"/>.</summary>
    public static ZipEntryStorage OnDisk(SafeFileHandle handle, long length, string spillPath)
        => new(null, handle, length, spillPath);

    public long Length { get; }

    /// <summary>Where the bytes were spilled, or null when they are in a buffer. The file system that created it deletes
    /// it; this type only closes the read handle it owns.</summary>
    public string? SpillPath { get; }

    /// <summary>
    /// A fresh cursor. A spilled entry shares one read handle the way a file on disk does, so a cursor is a position and
    /// nothing else; an in-memory entry copies nothing either, it points at the same buffer.
    /// </summary>
    public IVirtualFile File => _data is { } data ? new MemoryFile(data) : new DirectFileCursor(_handle!, 0, Length);

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
