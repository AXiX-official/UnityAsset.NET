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

    public void Serialize(DataBuffer db)
    {
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            db.WriteUInt32(0);
            db.WriteUInt32(0);
            db.WriteUInt32((uint)Version);
            db.WriteUInt32(0);
        }
        else
        {
            db.WriteUInt32(MetadataSize);
            db.WriteUInt32((uint)FileSize);
            db.WriteUInt32((uint)Version);
            db.WriteUInt32((uint)DataOffset);
        }
        
        db.WriteBoolean(Endianess);
        db.WriteBytes(Reserved);

        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            db.WriteUInt32(MetadataSize);
            db.WriteUInt64(FileSize);
            db.WriteUInt64(DataOffset);
            db.WriteInt64(Unknown); // unknown
        }
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