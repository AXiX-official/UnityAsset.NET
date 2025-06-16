using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class Header
{
    public string Signature;
    public UInt32 Version;
    public string UnityVersion;
    public string UnityRevision;
    public Int64 Size;
    public UInt32 CompressedBlocksInfoSize;
    public UInt32 UncompressedBlocksInfoSize;
    public ArchiveFlags Flags;
    
    public Header(string signature, UInt32 version, string unityVersion, string unityRevision,
                  Int64 size, UInt32 compressedBlocksInfoSize, UInt32 uncompressedBlocksInfoSize, 
                  ArchiveFlags flags)
    {
        Signature = signature;
        Version = version;
        UnityVersion = unityVersion;
        UnityRevision = unityRevision;
        Size = size;
        CompressedBlocksInfoSize = compressedBlocksInfoSize;
        UncompressedBlocksInfoSize = uncompressedBlocksInfoSize;
        Flags = flags;
    }
    
    public static Header Parse(DataBuffer db) => new (
        db.ReadNullTerminatedString(),
        db.ReadUInt32(),
        db.ReadNullTerminatedString(),
        db.ReadNullTerminatedString(),
        db.ReadInt64(),
        db.ReadUInt32(),
        db.ReadUInt32(),
        (ArchiveFlags)db.ReadUInt32()
    );

    public int Serialize(DataBuffer db)
    {
        int size = 0;
        size += db.WriteNullTerminatedString(Signature);
        size += db.WriteUInt32(Version);
        size += db.WriteNullTerminatedString(UnityVersion);
        size += db.WriteNullTerminatedString(UnityRevision);
        size += db.WriteInt64(Size);
        size += db.WriteUInt32(CompressedBlocksInfoSize);
        size += db.WriteUInt32(UncompressedBlocksInfoSize);
        size += db.WriteUInt32((UInt32)Flags);
        return size;
    }

    public long SerializeSize => 27 + Signature.Length + UnityVersion.Length + UnityRevision.Length;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Signature: {Signature} | ");
        sb.Append($"Version: {Version} | ");
        sb.Append($"UnityVersion: {UnityVersion} | ");
        sb.Append($"UnityRevision: {UnityRevision} | ");
        sb.Append($"Size: 0x{Size:X8} | ");
        sb.Append($"CompressedBlocksInfoSize: 0x{CompressedBlocksInfoSize:X8} | ");
        sb.Append($"UncompressedBlocksInfoSize: 0x{UncompressedBlocksInfoSize:X8} | ");
        sb.Append($"Flags: 0x{(int)Flags:X8}");
        return sb.ToString();
    }
}