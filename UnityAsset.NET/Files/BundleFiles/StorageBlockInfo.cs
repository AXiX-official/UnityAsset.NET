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
    
    public static StorageBlockInfo Parse(IReader reader) => new (
        reader.ReadUInt32(),
        reader.ReadUInt32(),
        (StorageBlockFlags)reader.ReadUInt16()
    );
    
    public void Serialize(IWriter writer) {
        writer.WriteUInt32(UncompressedSize);
        writer.WriteUInt32(CompressedSize);
        writer.WriteUInt16((UInt16)Flags);
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