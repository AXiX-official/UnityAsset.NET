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
        return 
            $"offset: 0x{offset:X8} | " + 
            $"size: 0x{size:X8} | " + 
            $"flags: {flags} | " + 
            $"path: {path}";
    }
        
    public long CalculateSize()
    {
        return 8 + 8 + 4 + path.Length + 1;
    }
}