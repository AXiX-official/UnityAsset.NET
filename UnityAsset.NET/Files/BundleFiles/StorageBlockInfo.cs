using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

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
        sb.Append($"UncompressedSize: 0x{UncompressedSize:X8} | ");
        sb.Append($"CompressedSize: 0x{CompressedSize:X8} | ");
        sb.Append($"Flags: 0x{(int)Flags:X4}");
        return sb.ToString();
    }
}