using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFileHeader
{
    public uint MetadataSize;
    public long FileSize;
    public SerializedFileFormatVersion Version;
    public long DataOffset;
    public byte Endianess;
    
    public SerializedFileHeader(AssetReader reader)
    {
        MetadataSize = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();
        Version = (SerializedFileFormatVersion)reader.ReadUInt32();
        DataOffset = reader.ReadUInt32();

        if (Version < SerializedFileFormatVersion.Unknown_9)
        {
            throw new Exception("Unsupported version.");
        }
        
        Endianess = reader.ReadByte();
        reader.ReadBytes(3);// unused bytes
        
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            MetadataSize = reader.ReadUInt32();
            FileSize = reader.ReadUInt32();
            DataOffset = reader.ReadUInt32();
            reader.ReadInt64(); // unknown
        }
        
        reader.BigEndian = Endianess == 1;
    }

    public override string ToString()
    {
        return $"MetadataSize: 0x{MetadataSize:X8} | FileSize: 0x{FileSize:X8} | Version: {Version} | DataOffset: 0x{DataOffset:X8} | Endianness: {(EndianType)Endianess}";
    }
}