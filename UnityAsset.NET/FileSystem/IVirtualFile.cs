using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.FileSystem;

public static class FileTypeHelper
{
    public static FileType GetFileType(IVirtualFile file)
    {
        using var stream = file.OpenStream();
        return GetFileType(stream);
    }
        
    public static FileType GetFileType(Stream stream)
    {
        
        IReader reader = new CustomStreamReader(stream);
        reader.Seek(0);
        if (stream.Length > 7)
        {
            var signature = reader.ReadBytes(7);
            var header = System.Text.Encoding.UTF8.GetString(signature);
            if (header == "UnityFS")
            {
                return FileType.BundleFile;
            }
            
            if (stream.Length < 20)
            {
                return FileType.Unknown;
            }
            
            reader.Seek(0);
            
            var metadataSize = reader.ReadUInt32();
            UInt64 fileSize = reader.ReadUInt32();
            var version = (SerializedFileFormatVersion)reader.ReadUInt32();
            UInt64 dataOffset = reader.ReadUInt32();
            var endianness = (Endianness)reader.ReadByte();
            var reserved = reader.ReadBytes(3);
            Int64 unknown = 0;
        
            if (version >= SerializedFileFormatVersion.LargeFilesSupport)
            {
                if (stream.Length < 48)
                {
                    reader.Seek(0);
                    return FileType.Unknown;
                }
                metadataSize = reader.ReadUInt32();
                fileSize = reader.ReadUInt64();
                dataOffset = reader.ReadUInt64();
                unknown = reader.ReadInt64(); // unknown
            }
            
            reader.Seek(0);
            if ((long)fileSize != stream.Length || (long)dataOffset > stream.Length)
            {
                return FileType.Unknown;
            }
            return FileType.SerializedFile;
        }
        return FileType.Unknown;
    }
}

public interface IVirtualFile
{
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }
    Stream OpenStream();
}