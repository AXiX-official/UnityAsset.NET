using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public class AssetFileInfo
{
    public Int64 PathId;
    public UInt64 ByteOffset;
    public UInt32 ByteSize;
    public Int32 TypeIdOrIndex;
    public UInt16? OldTypeId;
    public UInt16? ScriptTypeIndex;
    public byte? Stripped;
    public int TypeId;
    public byte[] Data;
    
    public AssetFileInfo(Int64 pathId, UInt64 byteOffset, UInt32 byteSize,
        int typeIdOrIndex, UInt16? oldTypeId, UInt16? scriptTypeIndex, byte? stripped)
    {
        PathId = pathId;
        ByteOffset = byteOffset;
        ByteSize = byteSize;
        TypeIdOrIndex = typeIdOrIndex;
        OldTypeId = oldTypeId;
        ScriptTypeIndex = scriptTypeIndex;
        Stripped = stripped;
    }

    public static AssetFileInfo Parse(ref DataBuffer db, SerializedFileFormatVersion version)
    {
        db.Align(4);
        var pathId = version >= SerializedFileFormatVersion.Unknown_14 ?
            db.ReadInt64() : db.ReadUInt32();
        var byteOffset = version >= SerializedFileFormatVersion.LargeFilesSupport ?
            db.ReadUInt64() : db.ReadUInt32();
        var byteSize = db.ReadUInt32();
        var typeIdOrIndex = db.ReadInt32();
        var oldTypeId = version <= SerializedFileFormatVersion.SupportsStrippedObject ?
            db.ReadUInt16() : (UInt16?)null;
        var scriptTypeIndex = version <= SerializedFileFormatVersion.RefactoredClassId ?
            db.ReadUInt16() : (UInt16?)null;
        var stripped = version == SerializedFileFormatVersion.SupportsStrippedObject || 
                        version == SerializedFileFormatVersion.RefactoredClassId ?
            db.ReadByte() : (byte?)null;
        return new AssetFileInfo(pathId, byteOffset, byteSize, typeIdOrIndex, oldTypeId, scriptTypeIndex, stripped);
    }
    
    public int GetTypeId(List<SerializedType> types, SerializedFileFormatVersion version)
    {
        if (version < SerializedFileFormatVersion.RefactoredClassId)
        {
            return TypeIdOrIndex;
        }
        else
        {
            if (TypeIdOrIndex >= types.Count)
            {
                throw new IndexOutOfRangeException("TypeIndex is larger than type tree count!");
            }
            return types[TypeIdOrIndex].TypeID;
        }
    }
    
    public void Serialize(ref DataBuffer db, SerializedFileFormatVersion version)
    {
        db.Align(4);
        if (version >= SerializedFileFormatVersion.Unknown_14)
        {
            db.WriteInt64(PathId);
        }
        else
        {
            db.WriteInt32((Int32)PathId);
        }
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            db.WriteUInt64(ByteOffset);
        }
        else
        {
            db.WriteInt32((Int32)ByteOffset);
        }
        db.WriteUInt32(ByteSize);
        db.WriteInt32(TypeIdOrIndex);
        if (version <= SerializedFileFormatVersion.SupportsStrippedObject)
        {
            db.WriteUInt16(OldTypeId!.Value);
        }
        if (version <= SerializedFileFormatVersion.RefactoredClassId)
        {
            db.WriteUInt16(ScriptTypeIndex!.Value);
        }
        if (version == SerializedFileFormatVersion.SupportsStrippedObject || 
            version == SerializedFileFormatVersion.RefactoredClassId)
        {
            db.WriteByte(Stripped!.Value);
        }
    }

    public long SerializeSize => 29;

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
        return sb.ToString();
    }
}