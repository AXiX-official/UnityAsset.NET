using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedFileHeader
{
    public UInt32 MetadataSize;
    public UInt64 FileSize;
    public SerializedFileFormatVersion Version;
    public UInt64 DataOffset;
    public Endianness Endianness;
    public byte[] Reserved;
    public Int64 Unknown;

    public SerializedFileHeader(UInt32 metadataSize, UInt64 fileSize, SerializedFileFormatVersion version,
        UInt64 dataOffset, Endianness endianness, byte[] reserved, Int64 unknown = 0)
    {
        MetadataSize = metadataSize;
        FileSize = fileSize;
        Version = version;
        DataOffset = dataOffset;
        Endianness = endianness;
        Reserved = reserved;
        Unknown = unknown;
    }

    /// <summary>
    /// How many bytes a header of this version occupies. The 32-bit layout fits in 20; the one that adds the 64-bit sizes
    /// needs 48.
    /// </summary>
    public static int SizeOf(SerializedFileFormatVersion version)
        => version >= SerializedFileFormatVersion.LargeFilesSupport ? 48 : 20;

    /// <summary>
    /// Reads a header out of the first bytes of a file, and is the only place the layout is written down: the prober that
    /// decides what a file is reads the same bytes through this, so the two cannot drift apart.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> head, [NotNullWhen(true)] out SerializedFileHeader? header)
    {
        header = null;
        if (head.Length < 20)
            return false;

        var metadataSize = BinaryPrimitives.ReadUInt32BigEndian(head);
        ulong fileSize = BinaryPrimitives.ReadUInt32BigEndian(head[4..]);
        var version = (SerializedFileFormatVersion)BinaryPrimitives.ReadUInt32BigEndian(head[8..]);
        ulong dataOffset = BinaryPrimitives.ReadUInt32BigEndian(head[12..]);
        var endianness = (Endianness)head[16];
        var reserved = head.Slice(17, 3).ToArray();
        long unknown = 0;

        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            if (head.Length < 48)
                return false;

            metadataSize = BinaryPrimitives.ReadUInt32BigEndian(head[20..]);
            fileSize = BinaryPrimitives.ReadUInt64BigEndian(head[24..]);
            dataOffset = BinaryPrimitives.ReadUInt64BigEndian(head[32..]);
            unknown = BinaryPrimitives.ReadInt64BigEndian(head[40..]);
        }

        header = new SerializedFileHeader(metadataSize, fileSize, version, dataOffset, endianness, reserved, unknown);
        return true;
    }

    public static SerializedFileHeader Parse(IReader reader)
    {
        var start = reader.Position;

        // The version sits at the same offset in both layouts, so it decides how many bytes to buffer; the fields
        // themselves are left to TryParse.
        Span<byte> prefix = stackalloc byte[20];
        reader.ReadExactly(prefix);

        var version = (SerializedFileFormatVersion)BinaryPrimitives.ReadUInt32BigEndian(prefix[8..]);
        if (version < SerializedFileFormatVersion.RefactorTypeData)
            throw new Exception($"Unsupported version: {version}. Only support 2017.x or later.");

        var size = SizeOf(version);
        var head = new byte[size];
        prefix.CopyTo(head);
        if (size > prefix.Length)
            reader.ReadExactly(head.AsSpan(prefix.Length));

        if (!TryParse(head, out var header))
            throw new InvalidDataException(
                $"A version {version} header is {size} bytes and could not be read from the file.");

        reader.Position = start + size;
        return header;
    }

    public void Serialize(IWriter writer)
    {
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32((uint)Version);
            writer.WriteUInt32(0);
        }
        else
        {
            writer.WriteUInt32(MetadataSize);
            writer.WriteUInt32((uint)FileSize);
            writer.WriteUInt32((uint)Version);
            writer.WriteUInt32((uint)DataOffset);
        }

        writer.WriteByte((byte)Endianness);
        writer.WriteBytes(Reserved);

        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            writer.WriteUInt32(MetadataSize);
            writer.WriteUInt64(FileSize);
            writer.WriteUInt64(DataOffset);
            writer.WriteInt64(Unknown); // unknown
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized File Header:");
        sb.Append($"MetadataSize: 0x{MetadataSize:X8} | ");
        sb.Append($"FileSize: 0x{FileSize:X8} | ");
        sb.Append($"Version: {Version} | ");
        sb.Append($"DataOffset: 0x{DataOffset:X8} | ");
        sb.Append($"Endianness: {Endianness}");
        return sb.ToString();
    }
}