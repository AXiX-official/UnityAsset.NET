using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.Tests.Support;

/// <summary>
/// The in-memory files the Core IO tests read through, shaped so the reader under test is the only thing varying.
/// </summary>
internal static class IoFiles
{
    /// <summary>A byte array presented as a file, with no probing of any kind.</summary>
    public static IVirtualFileInfo Raw(string name, byte[] bytes) => new RawFileInfo(name, bytes);

    /// <summary>A file whose backing buffer is shorter than the length it declares, so reads near the end come up short.</summary>
    public static IVirtualFileInfo Truncated(string name, byte[] backing, long declaredLength)
        => new DeclaredLengthFileInfo(name, backing, declaredLength);
}

/// <summary>
/// A file that holds the bytes and reports the same length, and does nothing else.
/// <para>
/// The point of it is what it does <em>not</em> do: no file type detection, so nothing reads the file before the test
/// does, and no handle, so a test is free to keep writing the underlying path while a reader holds these bytes.
/// </para>
/// </summary>
internal sealed class RawFileInfo : IVirtualFileInfo
{
    private readonly byte[] _bytes;

    public RawFileInfo(string name, byte[] bytes)
    {
        Name = name;
        Path = name;
        _bytes = bytes;
    }

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);
    public string Path { get; }
    public string Name { get; }
    public long Length => _bytes.Length;
    public FileType FileType => FileType.Unknown;

    public IVirtualFile GetFile() => new RawMemoryFile(_bytes);
}

/// <summary>One in-memory file, so a reader can hold a window of it and refill across calls.</summary>
internal sealed class RawMemoryFile : IVirtualFile
{
    private readonly byte[] _bytes;

    public RawMemoryFile(byte[] bytes) => _bytes = bytes;

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);
    public long Length => _bytes.Length;
    public long Position { get; set; }

    public uint Read(Span<byte> buffer, uint offset, uint count)
    {
        var available = (int)Math.Min(count, _bytes.Length - Position);
        if (available <= 0)
            return 0;

        _bytes.AsSpan((int)Position, available).CopyTo(buffer.Slice((int)offset, available));
        Position += available;
        return (uint)available;
    }

    public IVirtualFile Clone() => new RawMemoryFile(_bytes) { Position = Position };
}

/// <summary>Declares more bytes than it holds, which is the shape of a truncated or mis-sized file.</summary>
internal sealed class DeclaredLengthFileInfo : IVirtualFileInfo
{
    private readonly byte[] _backing;
    private readonly long _declaredLength;

    public DeclaredLengthFileInfo(string name, byte[] backing, long declaredLength)
    {
        Name = name;
        Path = name;
        _backing = backing;
        _declaredLength = declaredLength;
    }

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);
    public string Path { get; }
    public string Name { get; }
    public long Length => _declaredLength;
    public FileType FileType => FileType.Unknown;

    public IVirtualFile GetFile() => new ShortFile(_backing, _declaredLength);
}

/// <summary>A file that believes it is longer than the bytes it holds, so every read near the end comes back short.</summary>
internal sealed class ShortFile : IVirtualFile
{
    private readonly byte[] _backing;

    public ShortFile(byte[] backing, long length)
    {
        _backing = backing;
        Length = length;
    }

    public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);
    public long Length { get; }
    public long Position { get; set; }

    public uint Read(Span<byte> buffer, uint offset, uint count)
    {
        var available = (int)Math.Min(Math.Min(count, Length - Position), _backing.Length - Position);
        if (available <= 0)
            return 0;

        _backing.AsSpan((int)Position, available).CopyTo(buffer.Slice((int)offset, available));
        Position += available;
        return (uint)available;
    }

    public IVirtualFile Clone() => new ShortFile(_backing, Length) { Position = Position };
}