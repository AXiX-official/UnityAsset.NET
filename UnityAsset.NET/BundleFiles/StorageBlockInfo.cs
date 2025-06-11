using System.Runtime.Serialization;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFiles;

public sealed class StorageBlockInfo
{
    public UInt32 UncompressedSize;
    public UInt32 CompressedSize;
    public StorageBlockFlags Flags;

    public StorageBlockInfo(UInt32 uncompressedSize, UInt32 compressedSize, StorageBlockFlags flags)
    {
        UncompressedSize = uncompressedSize;
        CompressedSize = compressedSize;
        Flags = flags;
    }
    
    public static StorageBlockInfo ParseFromReader(AssetReader reader) => new (
        reader.ReadUInt32(),
        reader.ReadUInt32(),
        (StorageBlockFlags)reader.ReadUInt16()
    );
    
    public void Serialize(AssetWriter writer) {
        writer.WriteUInt32(UncompressedSize);
        writer.WriteUInt32(CompressedSize);
        writer.WriteUInt16((UInt16)Flags);
    }

    public long SerializeSize => 10;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("uncompressedSize: 0x{0:X8} | ", UncompressedSize);
        sb.AppendFormat("compressedSize: 0x{0:X8} | ", CompressedSize);
        sb.AppendFormat("flags: 0x{0:X4}", (int)Flags);
        return sb.ToString();
    }
}