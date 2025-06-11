using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFiles;

public sealed class CabInfo
{
    public Int64 Offset;
    public Int64 Size;
    public UInt32 Flags;
    public string Path;

    public CabInfo(Int64 offset, Int64 size, UInt32 flags, string path)
    {
        Offset = offset;
        Size = size;
        Flags = flags;
        Path = path;
    }
    
    public static CabInfo ParseFromReader(AssetReader reader) => new (
        reader.ReadInt64(),
        reader.ReadInt64(),
        reader.ReadUInt32(),
        reader.ReadStringToNull()
    );
    
    public void Serialize(AssetWriter writer) {
        writer.WriteInt64(Offset);
        writer.WriteInt64(Size);
        writer.WriteUInt32(Flags);
        writer.WriteStringToNull(Path);
    }

    public long SerializeSize => 21 + Path.Length;
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("offset: 0x{0:X8} | ", Offset);
        sb.AppendFormat("size: 0x{0:X8} | ", Size);
        sb.AppendFormat("flags: {0} | ", Flags);
        sb.AppendFormat("path: {0}", Path);
        return sb.ToString();
    }
}