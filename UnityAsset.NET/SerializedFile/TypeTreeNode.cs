using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public class TypeTreeNode
{
    public UInt16 Vesion;
    
    public byte Level;
    
    public TypeTreeNodeFlags TypeFlags;
    
    public uint TypeStringOffset;
    
    public uint NameStringOffset;
    
    public int ByteSize;
    
    public uint Index;
    
    public uint MetaFlags;
    
    public ulong RefTypeHash;
    
    public TypeTreeNode(AssetReader r, SerializedFileFormatVersion version)
    {
        Vesion = r.ReadUInt16();
        Level = r.ReadByte();
        TypeFlags = (TypeTreeNodeFlags)r.ReadByte();
        TypeStringOffset = r.ReadUInt32();
        NameStringOffset = r.ReadUInt32();
        ByteSize = r.ReadInt32();
        Index = r.ReadUInt32();
        MetaFlags = r.ReadUInt32();
        
        if (version >= SerializedFileFormatVersion.Unknown_12)
        {
            RefTypeHash = r.ReadUInt64();
        }
    }

    public void Write(AssetWriter w, SerializedFileFormatVersion version)
    {
        w.WriteUInt16(Vesion);
        w.Write(Level);
        w.Write((byte)TypeFlags);
        w.WriteUInt32(TypeStringOffset);
        w.WriteUInt32(NameStringOffset);
        w.WriteInt32(ByteSize);
        w.WriteUInt32(Index);
        w.WriteUInt32(MetaFlags);
        
        if (version >= SerializedFileFormatVersion.Unknown_12)
        {
            w.WriteUInt64(RefTypeHash);
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