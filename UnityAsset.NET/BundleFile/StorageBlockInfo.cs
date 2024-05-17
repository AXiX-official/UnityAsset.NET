using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class StorageBlockInfo
{
    public uint compressedSize;
    public uint uncompressedSize;
    public StorageBlockFlags flags;

    public StorageBlockInfo(uint compressedSize, uint uncompressedSize, StorageBlockFlags flags)
    {
        this.compressedSize = compressedSize;
        this.uncompressedSize = uncompressedSize;
        this.flags = flags;
    }
    
    public StorageBlockInfo(AssetReader reader)
    {
        uncompressedSize = reader.ReadUInt32();
        compressedSize = reader.ReadUInt32();
        flags = (StorageBlockFlags)reader.ReadUInt16();
    }
        
    public void Write(AssetWriter writer)
    {
        writer.WriteUInt32(uncompressedSize);
        writer.WriteUInt32(compressedSize);
        writer.WriteUInt16((ushort)flags);
    }

    public override string ToString()
    {
        return 
            $"compressedSize: 0x{compressedSize:X8} | " +
            $"uncompressedSize: 0x{uncompressedSize:X8} | " +
            $"flags: 0x{(int)flags:X8}";
    }
}