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
    public byte[] Reserved;
    
    public SerializedFileHeader(AssetReader reader)
    {
        MetadataSize = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();
        Version = (SerializedFileFormatVersion)reader.ReadUInt32();
        DataOffset = reader.ReadUInt32();

        if (Version >= SerializedFileFormatVersion.Unknown_9)
        {
            Endianess = reader.ReadByte();
            Reserved = reader.ReadBytes(3);
        }
        else
        {
            reader.Position = FileSize - MetadataSize;
            Endianess = reader.ReadByte();
        }
        
        if (Version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            MetadataSize = reader.ReadUInt32();
            FileSize = reader.ReadUInt32();
            DataOffset = reader.ReadUInt32();
            reader.ReadInt64(); // unknown
        }
    }
}