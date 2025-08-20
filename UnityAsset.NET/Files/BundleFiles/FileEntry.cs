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
    
    public static FileEntry Parse(IReader reader) => new (
        reader.ReadInt64(),
        reader.ReadInt64(),
        reader.ReadUInt32(),
        reader.ReadNullTerminatedString()
    );
    
    /*public void Serialize(IWriter writer)
    {
        writer.WriteInt64(Offset);
        writer.WriteInt64(Size);
        writer.WriteUInt32(Flags);
        writer.WriteNullTerminatedString(Path);
    }

    public long SerializeSize => 21 + Path.Length;*/
    
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