using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class Header
{
    public string Signature;
    public UInt32 Version;
    public string UnityVersion;
    public UnityRevision UnityRevision;
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
    
    public static Header Parse(IReader reader) => new (
        reader.ReadNullTerminatedString(),
        reader.ReadUInt32(),
        reader.ReadNullTerminatedString(),
        reader.ReadNullTerminatedString(),
        reader.ReadInt64(),
        reader.ReadUInt32(),
        reader.ReadUInt32(),
        (ArchiveFlags)reader.ReadUInt32()
    );

    /*public void Serialize(IWriter writer)
    {
        writer.WriteNullTerminatedString(Signature);
        writer.WriteUInt32(Version);
        writer.WriteNullTerminatedString(UnityVersion);
        writer.WriteNullTerminatedString(UnityRevision);
        writer.WriteInt64(Size);
        writer.WriteUInt32(CompressedBlocksInfoSize);
        writer.WriteUInt32(UncompressedBlocksInfoSize);
        writer.WriteUInt32((UInt32)Flags);
    }

    public long SerializeSize => 27 + Signature.Length + UnityVersion.Length + UnityRevision.Length;*/

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