using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class FileEntry
{
    public Int64 Offset;
    public Int64 Size;
    public UInt32 Flags;
    public string Path;

    public FileEntry(Int64 offset, Int64 size, UInt32 flags, string path)
    {
        Offset = offset;
        Size = size;
        Flags = flags;
        Path = path;
    }
    
    public static FileEntry Parse(DataBuffer db) => new (
        db.ReadInt64(),
        db.ReadInt64(),
        db.ReadUInt32(),
        db.ReadNullTerminatedString()
    );
    
    public int Serialize(DataBuffer db)
    {
        int size = 0;
        size += db.WriteInt64(Offset);
        size += db.WriteInt64(Size);
        size += db.WriteUInt32(Flags);
        size += db.WriteNullTerminatedString(Path);
        return size;
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