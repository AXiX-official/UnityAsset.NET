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
    
    public static StorageBlockInfo Parse(ref DataBuffer db) => new (
        db.ReadUInt32(),
        db.ReadUInt32(),
        (StorageBlockFlags)db.ReadUInt16()
    );
    
    public void Serialize(ref DataBuffer db) {
        db.WriteUInt32(UncompressedSize);
        db.WriteUInt32(CompressedSize);
        db.WriteUInt16((UInt16)Flags);
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