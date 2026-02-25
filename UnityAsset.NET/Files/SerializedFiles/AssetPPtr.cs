using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public class AssetPPtr
{
    public Int32 FileId;
    public Int64 PathId;
    
    public AssetPPtr(Int32 fileId, Int64 pathId)
    { 
        FileId = fileId;
        PathId = pathId;
    }

    public static AssetPPtr Parse(IReader reader)
    {
        var fileId = reader.ReadInt32();
        reader.Align(4);
        var pathId = reader.ReadInt64();
        return new AssetPPtr(fileId, pathId);
    }

    public void Serialize(IWriter writer)
    {
        writer.WriteInt32(FileId);
        writer.Align(4);
        writer.WriteInt64(PathId);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"AssetPPtr:");
        sb.AppendLine($"FileId: {FileId} | ");
        sb.AppendLine($"PathId: {PathId} | ");
        return sb.ToString();
    }
}