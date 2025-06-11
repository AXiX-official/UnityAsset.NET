using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public sealed class SerializedFileHeader
{
    public UInt32 MetadataSize;
    public UInt64 FileSize;
    public SerializedFileFormatVersion Version;
    public UInt64 DataOffset;
    public bool Endianess;
    public byte[] Reserved;
    public Int64 Unknown;
    
    public SerializedFileHeader(UInt32 metadataSize, UInt64 fileSize, SerializedFileFormatVersion version,
        UInt64 dataOffset, bool endianess, byte[] reserved, Int64 unknown = 0)
    {
        MetadataSize = metadataSize;
        FileSize = fileSize;
        Version = version;
        DataOffset = dataOffset;
        Endianess = endianess;
        Reserved = reserved;
        Unknown = unknown;
    }
    
    public static SerializedFileHeader ParseFromReader(AssetReader r)
    {
        var metadataSize = r.ReadUInt32();
        UInt64 fileSize = r.ReadUInt32();
        var version = (SerializedFileFormatVersion)r.ReadUInt32();
        UInt64 dataOffset = r.ReadUInt32();

        if (version < SerializedFileFormatVersion.Unknown_9)
        {
            throw new Exception($"Unsupported version: {version}");
        }
        
        var endianess = r.ReadBoolean();
        var reserved = r.ReadBytes(3);
        Int64 unknown = 0;
        
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            metadataSize = r.ReadUInt32();
            fileSize = r.ReadUInt64();
            dataOffset = r.ReadUInt64();
            unknown = r.ReadInt64(); // unknown
        }
        
        return new SerializedFileHeader(metadataSize, fileSize, version, dataOffset, endianess, reserved, unknown);
    }

    public void Serialize(AssetWriter writer)
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
        
        writer.WriteBoolean(Endianess);
        writer.Write(Reserved);

        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            writer.WriteUInt32(MetadataSize);
            writer.WriteUInt64(FileSize);
            writer.WriteUInt64(DataOffset);
            writer.WriteInt64(Unknown); // unknown
        }
    }

    public long SerializeSize()
    {
        return Version >= SerializedFileFormatVersion.LargeFilesSupport ? 20 : 48;
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized File Header:");
        sb.AppendFormat("MetadataSize: 0x{0:X8} | ", MetadataSize);
        sb.AppendFormat("FileSize: 0x{0:X8} | ", FileSize);
        sb.AppendFormat("Version: {0} | ", Version);
        sb.AppendFormat("DataOffset: 0x{0:X8} | ", DataOffset);
        sb.AppendFormat("Endianness: {0}", Endianess ? "BigEndian" : "LittleEndian");
        return sb.ToString();
    }
}