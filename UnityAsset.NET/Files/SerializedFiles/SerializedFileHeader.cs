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
    
    public static SerializedFileHeader Parse(DataBuffer db)
    {
        var metadataSize = db.ReadUInt32();
        UInt64 fileSize = db.ReadUInt32();
        var version = (SerializedFileFormatVersion)db.ReadUInt32();
        UInt64 dataOffset = db.ReadUInt32();

        if (version < SerializedFileFormatVersion.RefactorTypeData)
            throw new Exception($"Unsupported version: {version}. Only support 2017.x or later.");
        
        var endianess = db.ReadBoolean();
        var reserved = db.ReadBytes(3);
        Int64 unknown = 0;
        
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            metadataSize = db.ReadUInt32();
            fileSize = db.ReadUInt64();
            dataOffset = db.ReadUInt64();
            unknown = db.ReadInt64(); // unknown
        }
        
        return new SerializedFileHeader(metadataSize, fileSize, version, dataOffset, endianess, reserved, unknown);
    }

    public int Serialize(DataBuffer db)
    {
        int size = 0;
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            size += db.WriteUInt32(0);
            size += db.WriteUInt32(0);
            size += db.WriteUInt32((uint)Version);
            size += db.WriteUInt32(0);
        }
        else
        {
            size += db.WriteUInt32(MetadataSize);
            size += db.WriteUInt32((uint)FileSize);
            size += db.WriteUInt32((uint)Version);
            size += db.WriteUInt32((uint)DataOffset);
        }
        
        size += db.WriteBoolean(Endianess);
        size += db.WriteBytes(Reserved);

        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            size += db.WriteUInt32(MetadataSize);
            size += db.WriteUInt64(FileSize);
            size += db.WriteUInt64(DataOffset);
            size += db.WriteInt64(Unknown); // unknown
        }

        return size;
    }

    public long SerializeSize() => Version >= SerializedFileFormatVersion.LargeFilesSupport ? 20 : 48;
    
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