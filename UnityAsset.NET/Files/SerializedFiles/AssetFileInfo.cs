using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public class AssetFileInfo
{
    public Int64 PathId;
    public UInt64 ByteOffset;
    public UInt32 ByteSize;
    public Int32 TypeIdOrIndex;
    public SerializedType Type;
    
    public AssetFileInfo(Int64 pathId, UInt64 byteOffset, UInt32 byteSize,
        int typeIdOrIndex, SerializedType type)
    {
        PathId = pathId;
        ByteOffset = byteOffset;
        ByteSize = byteSize;
        TypeIdOrIndex = typeIdOrIndex;
        Type = type;
    }

    public static AssetFileInfo Parse(DataBuffer db, SerializedFileFormatVersion version, List<SerializedType> types)
    {
        db.Align(4);
        var pathId = db.ReadInt64();
        var byteOffset = version >= SerializedFileFormatVersion.LargeFilesSupport ?
            db.ReadUInt64() : db.ReadUInt32();
        var byteSize = db.ReadUInt32();
        var typeIdOrIndex = db.ReadInt32();
        if (typeIdOrIndex >= types.Count)
            throw new IndexOutOfRangeException("TypeIndex is larger than type tree count!");
        var type = types[typeIdOrIndex];
        return new AssetFileInfo(pathId, byteOffset, byteSize, typeIdOrIndex, type);
    }
    
    public void Serialize(DataBuffer db, SerializedFileFormatVersion version)
    {
        db.Align(4);
        db.WriteInt64(PathId);
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
            db.WriteUInt64(ByteOffset);
        else
            db.WriteInt32((Int32)ByteOffset);
        db.WriteUInt32(ByteSize);
        db.WriteInt32(TypeIdOrIndex);
    }

    public long SerializeSize => 28;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"PathId: {PathId} | ");
        sb.Append($"ByteOffset: {ByteOffset} | ");
        sb.Append($"ByteSize: {ByteSize} | ");
        sb.Append($"TypeIdOrIndex: {TypeIdOrIndex} | ");
        //sb.Append($"Type: {Type}");
        return sb.ToString();
    }
}