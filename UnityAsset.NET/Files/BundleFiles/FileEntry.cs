using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class FileEntry
{
    public ulong Offset;
    public ulong Size;
    public UInt32 Flags;
    public string Path;

    public FileEntry(ulong offset, ulong size, UInt32 flags, string path)
    {
        Offset = offset;
        Size = size;
        Flags = flags;
        Path = path;
    }
    
    public static FileEntry Parse(IReader reader) => new (
        reader.ReadUInt64(),
        reader.ReadUInt64(),
        reader.ReadUInt32(),
        reader.ReadNullTerminatedString()
    );
    
    public void Serialize(IWriter writer)
    {
        writer.WriteUInt64(Offset);
        writer.WriteUInt64(Size);
        writer.WriteUInt32(Flags);
        writer.WriteNullTerminatedString(Path);
    }
    
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