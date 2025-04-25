using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public class AssetPPtr
{
    public string FilePath;
    
    public int FileId;
    
    public long PathId;

    public AssetPPtr(AssetReader r)
    {
        FilePath = string.Empty;
        FileId = r.ReadInt32();
        r.Align(4);
        PathId = r.ReadInt64();
    }

    public void Write(AssetWriter w)
    {
        w.WriteInt32(FileId);
        w.Align(4);
        w.WriteInt64(PathId);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"AssetPPtr:");
        sb.AppendLine($"FileId: {FileId} | ");
        sb.AppendLine($"PathId: {PathId} | ");
        sb.AppendLine($"FilePath: {FilePath}");
        return sb.ToString();
    }
}