using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFileHeader
{
    public uint MetadataSize;
    public ulong FileSize;
    public SerializedFileFormatVersion Version;
    public ulong DataOffset;
    public bool Endianess;
    public byte[] Reserved;
    public Int64 Unknown;
    
    public SerializedFileHeader(AssetReader r)
    {
        MetadataSize = r.ReadUInt32();
        FileSize = r.ReadUInt32();
        Version = (SerializedFileFormatVersion)r.ReadUInt32();
        DataOffset = r.ReadUInt32();

        if (Version < SerializedFileFormatVersion.Unknown_9)
        {
            throw new Exception($"Unsupported version: {Version}");
        }
        
        Endianess = r.ReadBoolean();
        Reserved = r.ReadBytes(3);
        
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            MetadataSize = r.ReadUInt32();
            FileSize = r.ReadUInt64();
            DataOffset = r.ReadUInt64();
            Unknown = r.ReadInt64(); // unknown
        }
    }

    public void Write(AssetWriter w)
    {
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32((uint)Version);
            w.WriteUInt32(0);
        }
        else
        {
            w.WriteUInt32(MetadataSize);
            w.WriteUInt32((uint)FileSize);
            w.WriteUInt32((uint)Version);
            w.WriteUInt32((uint)DataOffset);
        }
        
        w.WriteBoolean(Endianess);
        w.Write(Reserved);

        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            w.WriteUInt32(MetadataSize);
            w.WriteUInt64(FileSize);
            w.WriteUInt64(DataOffset);
            w.WriteInt64(Unknown); // unknown
        }
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