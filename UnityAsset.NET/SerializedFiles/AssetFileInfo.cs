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
    public AssetReader DataReader;
    
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

    public static AssetFileInfo ParseFromReader(AssetReader reader, SerializedFileFormatVersion version)
    {
        reader.Align(4);
        var pathId = version >= SerializedFileFormatVersion.Unknown_14 ?
            reader.ReadInt64() : reader.ReadUInt32();
        var byteOffset = version >= SerializedFileFormatVersion.LargeFilesSupport ?
            reader.ReadUInt64() : reader.ReadUInt32();
        var byteSize = reader.ReadUInt32();
        var typeIdOrIndex = reader.ReadInt32();
        var oldTypeId = version <= SerializedFileFormatVersion.SupportsStrippedObject ?
            reader.ReadUInt16() : (UInt16?)null;
        var scriptTypeIndex = version <= SerializedFileFormatVersion.RefactoredClassId ?
            reader.ReadUInt16() : (UInt16?)null;
        var stripped = version == SerializedFileFormatVersion.SupportsStrippedObject || 
                        version == SerializedFileFormatVersion.RefactoredClassId ?
            reader.ReadByte() : (byte?)null;
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
    
    public void Serialize(AssetWriter writer, SerializedFileFormatVersion version)
    {
        writer.Align(4);
        if (version >= SerializedFileFormatVersion.Unknown_14)
        {
            writer.WriteInt64(PathId);
        }
        else
        {
            writer.WriteInt32((Int32)PathId);
        }
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            writer.WriteUInt64(ByteOffset);
        }
        else
        {
            writer.WriteInt32((Int32)ByteOffset);
        }
        writer.WriteUInt32(ByteSize);
        writer.WriteInt32(TypeIdOrIndex);
        if (version <= SerializedFileFormatVersion.SupportsStrippedObject)
        {
            writer.WriteUInt16(OldTypeId!.Value);
        }
        if (version <= SerializedFileFormatVersion.RefactoredClassId)
        {
            writer.WriteUInt16(ScriptTypeIndex!.Value);
        }
        if (version == SerializedFileFormatVersion.SupportsStrippedObject || 
            version == SerializedFileFormatVersion.RefactoredClassId)
        {
            writer.Write(Stripped!.Value);
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
        return sb.ToString();
    }
}