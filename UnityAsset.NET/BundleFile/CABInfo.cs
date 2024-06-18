using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class CABInfo
{
    public long offset;
    public long size;
    public uint flags;
    public string path;

    public CABInfo(AssetReader reader)
    {
        offset = reader.ReadInt64();
        size = reader.ReadInt64();
        flags = reader.ReadUInt32();
        path = reader.ReadStringToNull();
    }
        
    public void Write(AssetWriter writer)
    {
        writer.WriteInt64(offset);
        writer.WriteInt64(size);
        writer.WriteUInt32(flags);
        writer.WriteStringToNull(path);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("offset: 0x{0:X8} | ", offset);
        sb.AppendFormat("size: 0x{0:X8} | ", size);
        sb.AppendFormat("flags: {0} | ", flags);
        sb.AppendFormat("path: {0}", path);
        return sb.ToString();
    }
        
    public long CalculateSize()
    {
        return 8 + 8 + 4 + path.Length + 1;
    }
}