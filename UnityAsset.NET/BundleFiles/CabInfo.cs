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
        sb.AppendFormat("offset: 0x{0:X8} | ", Offset);
        sb.AppendFormat("size: 0x{0:X8} | ", Size);
        sb.AppendFormat("flags: {0} | ", Flags);
        sb.AppendFormat("path: {0}", Path);
        return sb.ToString();
    }
}