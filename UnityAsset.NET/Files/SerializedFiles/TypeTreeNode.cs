using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

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
    public string Type;
    public string Name;
    
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

    public static TypeTreeNode Parse(DataBuffer db) => new(
        db.ReadUInt16(),
        db.ReadByte(),
        (TypeTreeNodeFlags)db.ReadByte(),
        db.ReadUInt32(),
        db.ReadUInt32(),
        db.ReadInt32(),
        db.ReadUInt32(),
        db.ReadUInt32(),
        db.ReadUInt64()
    );

    public int Serialize(DataBuffer db)
    {
        db.WriteUInt16(Vesion);
        db.WriteByte(Level);
        db.WriteByte((byte)TypeFlags);
        db.WriteUInt32(TypeStringOffset);
        db.WriteUInt32(NameStringOffset);
        db.WriteInt32(ByteSize);
        db.WriteUInt32(Index);
        db.WriteUInt32(MetaFlags); 
        db.WriteUInt64(RefTypeHash);
        return 32;
    }

    public long SerializeSize => 32;
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Version: {Vesion} | ");
        sb.Append($"Level: {Level} | ");
        sb.Append($"TypeFlags: {TypeFlags} | ");
        sb.Append($"TypeStringOffset: {TypeStringOffset} | ");
        sb.Append($"Type: {Type} | ");
        sb.Append($"NameStringOffset: {NameStringOffset} | ");
        sb.Append($"Name: {Name} | ");
        sb.Append($"ByteSize: {ByteSize} | ");
        sb.Append($"Index: {Index} | ");
        sb.Append($"MetaFlags: {MetaFlags} | ");
        sb.Append($"RefTypeHash: {RefTypeHash}");
        return sb.ToString();
    }
}