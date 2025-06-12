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

    public static TypeTreeNode Parse(ref DataBuffer db, SerializedFileFormatVersion version) => new(
        db.ReadUInt16(),
        db.ReadByte(),
        (TypeTreeNodeFlags)db.ReadByte(),
        db.ReadUInt32(),
        db.ReadUInt32(),
        db.ReadInt32(),
        db.ReadUInt32(),
        db.ReadUInt32(),
        version >= SerializedFileFormatVersion.Unknown_12 ? db.ReadUInt64() : 0
    );

    public void Serialize(ref DataBuffer db, SerializedFileFormatVersion version)
    {
        db.WriteUInt16(Vesion);
        db.WriteByte(Level);
        db.WriteByte((byte)TypeFlags);
        db.WriteUInt32(TypeStringOffset);
        db.WriteUInt32(NameStringOffset);
        db.WriteInt32(ByteSize);
        db.WriteUInt32(Index);
        db.WriteUInt32(MetaFlags);
        
        if (version >= SerializedFileFormatVersion.Unknown_12)
        {
            db.WriteUInt64(RefTypeHash);
        }
    }

    public long SerializeSize => 24;
    
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