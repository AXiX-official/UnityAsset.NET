using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.IO.Writer;

public interface IAssetWriter : IWriter
{
    SerializedFile AssetsFile { get; }
}

public class AssetWriter : CustomStreamWriter, IAssetWriter
{
    public SerializedFile AssetsFile { get; }

    public AssetWriter(Stream stream, SerializedFile assetsFile, Endianness endian = Endianness.BigEndian,
        bool leaveOpen = false, int bufferSize = 8192) : base(stream, endian, leaveOpen, bufferSize)
    {
        AssetsFile = assetsFile;
    }
}

/// <summary>
/// Gives any <see cref="IWriter"/> the asset context: a counting pass and a plain stream writer both reach the
/// version-dependent members without having to be a buffered stream writer.
/// </summary>
public sealed class AssetWriterProxy : IAssetWriter
{
    private readonly IWriter _inner;

    public SerializedFile AssetsFile { get; }

    public AssetWriterProxy(IWriter inner, SerializedFile assetsFile)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        AssetsFile = assetsFile ?? throw new ArgumentNullException(nameof(assetsFile));
    }

    public long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public long Length => _inner.Length;

    public Endianness Endian
    {
        get => _inner.Endian;
        set => _inner.Endian = value;
    }

    public void Align(uint alignment) => _inner.Align(alignment);

    public void WriteByte(byte value) => _inner.WriteByte(value);
    
    public void WriteSByte(sbyte value) => _inner.WriteSByte(value);
    public void WriteInt8(sbyte value) => _inner.WriteInt8(value);
    public void WriteUInt8(byte value) => _inner.WriteUInt8(value);
    public void WriteBoolean(bool value) => _inner.WriteBoolean(value);
    public void WriteChar(char value) => _inner.WriteChar(value);
    public void WriteInt16(short value) => _inner.WriteInt16(value);
    public void WriteUInt16(ushort value) => _inner.WriteUInt16(value);
    public void WriteInt32(int value) => _inner.WriteInt32(value);
    public void WriteUInt32(uint value) => _inner.WriteUInt32(value);
    public void WriteInt64(long value) => _inner.WriteInt64(value);
    public void WriteUInt64(ulong value) => _inner.WriteUInt64(value);
    public void WriteSingle(float value) => _inner.WriteSingle(value);
    public void WriteDouble(double value) => _inner.WriteDouble(value);
    public void WriteBytes(byte value, ulong count) => _inner.WriteBytes(value, count);
    public void WriteNullTerminatedString(string value) => _inner.WriteNullTerminatedString(value);
    public void WriteSizedString(string value) => _inner.WriteSizedString(value);

    public void WriteBytes(ReadOnlySpan<byte> bytes) => _inner.WriteBytes(bytes);

    public ulong WriteBytes(IReader reader) => _inner.WriteBytes(reader);

    public void WriteStream(Stream stream) => _inner.WriteStream(stream);
}
