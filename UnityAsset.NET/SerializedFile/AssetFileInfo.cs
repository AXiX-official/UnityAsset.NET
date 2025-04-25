using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public class AssetFileInfo
{
    public long PathId;
    
    public long ByteOffset;
    
    public uint ByteSize;
    
    public int TypeIdOrIndex;
    
    public ushort OldTypeId;
    
    public ushort ScriptTypeIndex;
    
    public byte Stripped;
    
    public int TypeId;

    public AssetFileInfo(AssetReader r, SerializedFileFormatVersion version)
    {
        r.Align(4);
        PathId = version >= SerializedFileFormatVersion.Unknown_14 ?
            r.ReadInt64() : r.ReadUInt32();
        ByteOffset = version >= SerializedFileFormatVersion.LargeFilesSupport ?
            r.ReadInt64() : r.ReadUInt32();
        ByteSize = r.ReadUInt32();
        TypeIdOrIndex = r.ReadInt32();
        if (version <= SerializedFileFormatVersion.SupportsStrippedObject)
        {
            OldTypeId = r.ReadUInt16();
        }
        if (version <= SerializedFileFormatVersion.RefactoredClassId)
        {
            ScriptTypeIndex = r.ReadUInt16();
        }
        if (version == SerializedFileFormatVersion.SupportsStrippedObject || 
            version == SerializedFileFormatVersion.RefactoredClassId)
        {
            Stripped = r.ReadByte();
        }
    }
    
    public int GetTypeId(SerializedFileMetadata metadata, SerializedFileFormatVersion version)
    {
        return GetTypeId(metadata.Types, version);
    }
    
    public int GetTypeId(List<SerializedType> Types, SerializedFileFormatVersion version)
    {
        if (version < SerializedFileFormatVersion.RefactoredClassId)
        {
            return TypeIdOrIndex;
        }
        else
        {
            if (TypeIdOrIndex >= Types.Count)
            {
                throw new IndexOutOfRangeException("TypeIndex is larger than type tree count!");
            }
            return Types[TypeIdOrIndex].TypeID;
        }
    }
    
    public void Write(AssetWriter w, SerializedFileFormatVersion version)
    {
        w.Align(4);
        if (version >= SerializedFileFormatVersion.Unknown_14)
        {
            w.WriteInt64(PathId);
        }
        else
        {
            w.WriteInt32((Int32)PathId);
        }
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            w.WriteInt64(ByteOffset);
        }
        else
        {
            w.WriteInt32((Int32)ByteOffset);
        }
        w.WriteUInt32(ByteSize);
        w.WriteInt32(TypeIdOrIndex);
        if (version <= SerializedFileFormatVersion.SupportsStrippedObject)
        {
            w.WriteUInt16(OldTypeId);
        }
        if (version <= SerializedFileFormatVersion.RefactoredClassId)
        {
            w.WriteUInt16(ScriptTypeIndex);
        }
        if (version == SerializedFileFormatVersion.SupportsStrippedObject || 
            version == SerializedFileFormatVersion.RefactoredClassId)
        {
            w.Write(Stripped);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("PathId: {0} | ", PathId);
        sb.AppendFormat("ByteOffset: {0} | ", ByteOffset);
        sb.AppendFormat("ByteSize: {0} | ", ByteSize);
        sb.AppendFormat("TypeIdOrIndex: {0} | ", TypeIdOrIndex);
        sb.AppendFormat("OldTypeId: {0} | ", OldTypeId);
        sb.AppendFormat("ScriptTypeIndex: {0} | ", ScriptTypeIndex);
        sb.AppendFormat("Stripped: {0} | ", Stripped);
        sb.AppendFormat("TypeId: {0} | ", TypeId);
        return sb.ToString();
    }
}