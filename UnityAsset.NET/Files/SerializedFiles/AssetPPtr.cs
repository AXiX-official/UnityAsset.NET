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

    public static AssetPPtr Parse(DataBuffer db)
    {
        var fileId = db.ReadInt32();
        db.Align(4);
        var pathId = db.ReadInt64();
        return new AssetPPtr(fileId, pathId);
    }

    public int Serialize(DataBuffer db)
    {
        int size = 0;
        size += db.WriteInt32(FileId);
        size += db.Align(4);
        size += db.WriteInt64(PathId);
        return size;
    }

    public long SerializeSize => 16;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"AssetPPtr:");
        sb.AppendLine($"FileId: {FileId} | ");
        sb.AppendLine($"PathId: {PathId} | ");
        return sb.ToString();
    }
}