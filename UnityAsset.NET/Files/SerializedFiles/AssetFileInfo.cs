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

    public static AssetFileInfo Parse(IReader reader, SerializedFileFormatVersion version, SerializedType[] types)
    {
        reader.Align(4);
        var pathId = reader.ReadInt64();
        var byteOffset = version >= SerializedFileFormatVersion.LargeFilesSupport ?
            reader.ReadUInt64() : reader.ReadUInt32();
        var byteSize = reader.ReadUInt32();
        var typeIdOrIndex = reader.ReadInt32();
        if (typeIdOrIndex >= types.Length)
            throw new IndexOutOfRangeException("TypeIndex is larger than type tree count!");
        var type = types[typeIdOrIndex];
        return new AssetFileInfo(pathId, byteOffset, byteSize, typeIdOrIndex, type);
    }
    
    public void Serialize(IWriter writer, SerializedFileFormatVersion version)
    {
        writer.Align(4);
        writer.WriteInt64(PathId);
        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
            writer.WriteUInt64(ByteOffset);
        else
            writer.WriteInt32((Int32)ByteOffset);
        writer.WriteUInt32(ByteSize);
        writer.WriteInt32(TypeIdOrIndex);
    }

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