using Microsoft.Win32.SafeHandles;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// One <see cref="IReader"/> implementation under test, identified by name so a failure names the implementation.
/// <para>
/// The type is public and carries a name rather than a delegate because that is what xUnit's <c>MemberData</c> can
/// round-trip: a nested internal delegate row is silently dropped at discovery, which would turn a whole contract suite
/// into zero tests without failing anything.
/// </para>
/// </summary>
public sealed record ReaderImpl(string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// The implementations of <see cref="IReader"/> that the contract tests run against, and the fixtures they need.
/// <para>
/// A contract test is written once and run against every entry here. That is what keeps the implementations from drifting
/// apart: the same call with the same arguments has to answer the same way, and a difference becomes a deliberate
/// decision recorded in the case data instead of a surprise in production.
/// </para>
/// </summary>
public static class ReaderFactory
{
    public static ReaderImpl MemoryReader { get; } = new("MemoryReader");

    /// <summary>A 16-byte window: smaller than most fixtures, so a read has to refill instead of returning one window.</summary>
    public static ReaderImpl CustomFileReader { get; } = new("CustomFileReader");

    public static ReaderImpl SlicedReader { get; } = new("SlicedReader");

    /// <summary>A single uncompressed block, so its byte layout matches the dense fixtures the other three use.</summary>
    public static ReaderImpl BlockReader { get; } = new("BlockReader");

    /// <summary>Every implementation the byte-array contract matrix runs against.</summary>
    public static IReadOnlyList<ReaderImpl> All { get; } =
    [
        MemoryReader,
        CustomFileReader,
        SlicedReader,
        BlockReader,
    ];

    public static TheoryData<ReaderImpl> AllData
    {
        get
        {
            var data = new TheoryData<ReaderImpl>();
            foreach (var impl in All)
                data.Add(impl);

            return data;
        }
    }

    /// <summary>Builds the implementation over the given bytes, positioned at the start.</summary>
    public static IReader Create(this ReaderImpl impl, byte[] bytes) => impl.Name switch
    {
        "MemoryReader" => new MemoryReader(bytes),
        "CustomFileReader" => new CustomFileReader(RawFile("payload.bin", bytes), bufferSize: 16),
        "SlicedReader" => new SlicedReader(new MemoryReaderProvider(bytes), 0, (ulong)bytes.Length),
        "BlockReader" => new BlockReaderProvider(
            [new StorageBlockInfo((uint)bytes.Length, (uint)bytes.Length, (StorageBlockFlags)CompressionType.None)],
            new ByteFile(bytes)).CreateReader(),
        _ => throw new ArgumentOutOfRangeException(nameof(impl), impl.Name, "Unknown reader implementation."),
    };

    /// <summary>A payload of <paramref name="length"/> bytes with a distinct, non-zero value per index.</summary>
    public static byte[] Payload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i + 1);   // 1-based and never zero, so "nothing was written" is visible

        return payload;
    }

    /// <summary>
    /// The bytes as a file, with no probing of any kind. The existing <c>InMemoryFileInfo</c> runs type detection in its
    /// constructor, which reads the file before the test does and would perturb any assertion about read counts.
    /// </summary>
    public static IVirtualFileInfo RawFile(string name, byte[] bytes) => new RawFileInfo(name, bytes);

    private sealed class RawFileInfo : IVirtualFileInfo
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

        public IVirtualFile GetFile() => new ByteFile(_bytes);
    }

    /// <summary>A file whose bytes are held in memory, so nothing is read from the disk and no window is refilled oddly.</summary>
    private sealed class ByteFile : IVirtualFile
    {
        private readonly byte[] _data;

        public ByteFile(byte[] data) => _data = data;

        public SafeFileHandle Handle { get; } = new(IntPtr.Zero, ownsHandle: false);
        public long Length => _data.Length;
        public long Position { get; set; }

        public uint Read(Span<byte> buffer, uint offset, uint count)
        {
            var available = (int)Math.Min(count, _data.Length - Position);
            if (available <= 0)
                return 0;

            _data.AsSpan((int)Position, available).CopyTo(buffer.Slice((int)offset, available));
            Position += available;
            return (uint)available;
        }

        public IVirtualFile Clone() => new ByteFile(_data) { Position = Position };
    }
}