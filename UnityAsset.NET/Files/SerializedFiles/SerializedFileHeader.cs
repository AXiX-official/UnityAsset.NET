using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
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
    
    public static SerializedFileHeader Parse(IReader reader)
    {
        var metadataSize = reader.ReadUInt32();
        UInt64 fileSize = reader.ReadUInt32();
        var version = (SerializedFileFormatVersion)reader.ReadUInt32();
        UInt64 dataOffset = reader.ReadUInt32();

        if (version < SerializedFileFormatVersion.RefactorTypeData)
            throw new Exception($"Unsupported version: {version}. Only support 2017.x or later.");
        
        var endianness = (Endianness)reader.ReadByte();
        var reserved = reader.ReadBytes(3);
        Int64 unknown = 0;
        
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            metadataSize = reader.ReadUInt32();
            fileSize = reader.ReadUInt64();
            dataOffset = reader.ReadUInt64();
            unknown = reader.ReadInt64(); // unknown
        }
        
        return new SerializedFileHeader(metadataSize, fileSize, version, dataOffset, endianness, reserved, unknown);
    }

    /*public void Serialize(IWriter writer)
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

    public long SerializeSize() => Version >= SerializedFileFormatVersion.LargeFilesSupport ? 20 : 48;
    */
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