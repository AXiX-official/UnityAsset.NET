using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public class TypeTreeNode
{
    public UInt16 Vesion;
    public byte Level;
    public TypeTreeNodeFlags TypeFlags;
    public UInt32 TypeStringOffset;
    public UInt32 NameStringOffset;
    public Int32 ByteSize;
    public UInt32 Index;
    public UInt32 MetaFlags;
    public UInt64 RefTypeHash;
    
    public TypeTreeNode(UInt16 vesion, byte level, TypeTreeNodeFlags typeFlags,
        UInt32 typeStringOffset, UInt32 nameStringOffset, Int32 byteSize, UInt32 index, UInt32 metaFlags,
        UInt64 refTypeHash = 0)
    {
        Vesion = vesion;
        Level = level;
        TypeFlags = typeFlags;
        TypeStringOffset = typeStringOffset;
        NameStringOffset = nameStringOffset;
        ByteSize = byteSize;
        Index = index;
        MetaFlags = metaFlags;
        RefTypeHash = refTypeHash;
    }

    public static TypeTreeNode ParseFromReader(AssetReader r, SerializedFileFormatVersion version) => new(
        r.ReadUInt16(),
        r.ReadByte(),
        (TypeTreeNodeFlags)r.ReadByte(),
        r.ReadUInt32(),
        r.ReadUInt32(),
        r.ReadInt32(),
        r.ReadUInt32(),
        r.ReadUInt32(),
        version >= SerializedFileFormatVersion.Unknown_12 ? r.ReadUInt64() : 0
    );

    public void Serialize(AssetWriter writer, SerializedFileFormatVersion version)
    {
        writer.WriteUInt16(Vesion);
        writer.Write(Level);
        writer.Write((byte)TypeFlags);
        writer.WriteUInt32(TypeStringOffset);
        writer.WriteUInt32(NameStringOffset);
        writer.WriteInt32(ByteSize);
        writer.WriteUInt32(Index);
        writer.WriteUInt32(MetaFlags);
        
        if (version >= SerializedFileFormatVersion.Unknown_12)
        {
            writer.WriteUInt64(RefTypeHash);
        }
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Type Tree Node:");
        sb.AppendLine($"Version: {Vesion}");
        sb.AppendLine($"Level: {Level}");
        sb.AppendLine($"TypeFlags: {TypeFlags}");
        sb.AppendLine($"TypeStringOffset: {TypeStringOffset}");
        sb.AppendLine($"NameStringOffset: {NameStringOffset}");
        sb.AppendLine($"ByteSize: {ByteSize}");
        sb.AppendLine($"Index: {Index}");
        sb.AppendLine($"MetaFlags: {MetaFlags}");
        sb.AppendLine($"RefTypeHash: {RefTypeHash}");
        return sb.ToString();
    }
}