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
    
    public static CabInfo Parse(ref DataBuffer db) => new (
        db.ReadInt64(),
        db.ReadInt64(),
        db.ReadUInt32(),
        db.ReadNullTerminatedString()
    );
    
    public void Serialize(ref DataBuffer db) {
        db.WriteInt64(Offset);
        db.WriteInt64(Size);
        db.WriteUInt32(Flags);
        db.WriteNullTerminatedString(Path);
    }

    public long SerializeSize => 21 + Path.Length;
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Offset: 0x{Offset:X8} | ");
        sb.Append($"Size: 0x{Size:X8} | ");
        sb.Append($"Flags: {Flags} | ");
        sb.Append($"Path: {Path}");
        return sb.ToString();
    }
}