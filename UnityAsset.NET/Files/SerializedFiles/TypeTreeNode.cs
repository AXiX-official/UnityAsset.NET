using System.Collections.Concurrent;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Files.SerializedFiles;

public class TypeTreeNode
{
    public UInt16 Version;
    public byte Level;
    public TypeTreeNodeFlags TypeFlags;
    public UInt32 TypeStringOffset;
    public UInt32 NameStringOffset;
    public Int32 ByteSize;
    public UInt32 Index;
    public UInt32 MetaFlags;
    public UInt64 RefTypeHash;
    public string Type = String.Empty;
    public string Name = String.Empty;
    
    public static ConcurrentDictionary<Hash128, (TypeTreeNode[] Nodes, Int32 TypeID, TypeTreeRepr? typeRepr)> Cache = new();

    public static (TypeTreeNode[], bool) GetOrAdd(Hash128 key, (TypeTreeNode[] Nodes, Int32 TypeID) value)
    {
        var cacheVale =  Cache.GetOrAdd(key, k =>
        {
            var nodes = value.Nodes;
            if (nodes.Length > 0)
                return (value.Nodes, value.TypeID, nodes[0].ToTypeTreeRepr(nodes));
        
            return (value.Nodes, value.TypeID, null);
        });
        return (cacheVale.Nodes, cacheVale.typeRepr?.IsNamed ?? false);
    }
    
    public TypeTreeNode(UInt16 version, byte level, TypeTreeNodeFlags typeFlags,
        UInt32 typeStringOffset, UInt32 nameStringOffset, Int32 byteSize, UInt32 index, UInt32 metaFlags,
        UInt64 refTypeHash = 0)
    {
        Version = version;
        Level = level;
        TypeFlags = typeFlags;
        TypeStringOffset = typeStringOffset;
        NameStringOffset = nameStringOffset;
        ByteSize = byteSize;
        Index = index;
        MetaFlags = metaFlags;
        RefTypeHash = refTypeHash;
    }

    public static TypeTreeNode Parse(IReader reader) => new(
        reader.ReadUInt16(),
        reader.ReadByte(),
        (TypeTreeNodeFlags)reader.ReadByte(),
        reader.ReadUInt32(),
        reader.ReadUInt32(),
        reader.ReadInt32(),
        reader.ReadUInt32(),
        reader.ReadUInt32(),
        reader.ReadUInt64()
    );

    /*public void Serialize(IWriter writer)
    {
        writer.WriteUInt16(Version);
        writer.WriteByte(Level);
        writer.WriteByte((byte)TypeFlags);
        writer.WriteUInt32(TypeStringOffset);
        writer.WriteUInt32(NameStringOffset);
        writer.WriteInt32(ByteSize);
        writer.WriteUInt32(Index);
        writer.WriteUInt32(MetaFlags); 
        writer.WriteUInt64(RefTypeHash);
    }

    public long SerializeSize => 32;*/
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Version: {Version} | ");
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