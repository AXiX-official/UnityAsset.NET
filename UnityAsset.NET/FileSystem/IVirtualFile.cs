using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.FileSystem;

public static class FileTypeHelper
{
    public static FileType GetFileType(IVirtualFile file)
    {
        var stream = file.Stream;
        IReader reader = new CustomStreamReader(stream);
        reader.Seek(0);
        if (file.Length > 7)
        {
            var signature = reader.ReadBytes(7);
            var header = System.Text.Encoding.UTF8.GetString(signature);
            if (header == "UnityFS")
            {
                reader.Seek(0);
                return FileType.BundleFile;
            }
            
            if (file.Length < 20)
            {
                reader.Seek(0);
                return FileType.Unknown;
            }
            
            var metadataSize = reader.ReadUInt32();
            UInt64 fileSize = reader.ReadUInt32();
            var version = (SerializedFileFormatVersion)reader.ReadUInt32();
            UInt64 dataOffset = reader.ReadUInt32();
            var endianness = (Endianness)reader.ReadByte();
            var reserved = reader.ReadBytes(3);
            Int64 unknown = 0;
        
            if (version >= SerializedFileFormatVersion.LargeFilesSupport)
            {
                if (file.Length < 48)
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
            if ((long)fileSize != file.Length || (long)dataOffset > file.Length)
            {
                return FileType.Unknown;
            }
            return FileType.SerializedFile;
        }
        return FileType.Unknown;
    }
}

public interface IVirtualFile : IDisposable
{
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }
    public Stream Stream { get; }
    long Length { get; }
}