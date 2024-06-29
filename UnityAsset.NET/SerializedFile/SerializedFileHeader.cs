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
    
    public SerializedFileHeader(AssetReader reader)
    {
        MetadataSize = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();
        Version = (SerializedFileFormatVersion)reader.ReadUInt32();
        DataOffset = reader.ReadUInt32();

        if (Version < SerializedFileFormatVersion.Unknown_9)
        {
            throw new Exception($"Unsupported version: {Version}");
        }
        
        Endianess = reader.ReadBoolean();
        reader.ReadBytes(3);// unused bytes
        
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            MetadataSize = reader.ReadUInt32();
            FileSize = reader.ReadUInt64();
            DataOffset = reader.ReadUInt64();
            reader.ReadInt64(); // unknown
        }
        
        reader.BigEndian = Endianess;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("MetadataSize: 0x{0:X8} | ", MetadataSize);
        sb.AppendFormat("FileSize: 0x{0:X8} | ", FileSize);
        sb.AppendFormat("Version: {0} | ", Version);
        sb.AppendFormat("DataOffset: 0x{0:X8} | ", DataOffset);
        sb.AppendFormat("Endianness: {0}", Endianess ? "BigEndian" : "LittleEndian");
        return sb.ToString();
    }
}