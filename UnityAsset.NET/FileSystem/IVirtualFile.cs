using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.FileSystem;

public static class FileTypeHelper
{
    public static FileType GetFileType(IStreamProvider streamProvider)
    {
        using var reader = new CustomStreamReader(streamProvider);
        if (reader.Length > 7)
        {
            var signature = reader.ReadBytes(7);
            var header = System.Text.Encoding.UTF8.GetString(signature);
            if (header == "UnityFS")
            {
                return FileType.BundleFile;
            }
            
            if (reader.Length < 20)
            {
                return FileType.Unknown;
            }

            reader.Position = 0;
            
            var metadataSize = reader.ReadUInt32();
            UInt64 fileSize = reader.ReadUInt32();
            var version = (SerializedFileFormatVersion)reader.ReadUInt32();
            UInt64 dataOffset = reader.ReadUInt32();
            var endianness = (Endianness)reader.ReadByte();
            var reserved = reader.ReadBytes(3);
            Int64 unknown = 0;
        
            if (version >= SerializedFileFormatVersion.LargeFilesSupport)
            {
                if (reader.Length < 48)
                {
                    ((IReader)reader).Seek(0);
                    return FileType.Unknown;
                }
                metadataSize = reader.ReadUInt32();
                fileSize = reader.ReadUInt64();
                dataOffset = reader.ReadUInt64();
                unknown = reader.ReadInt64(); // unknown
            }
            
            if ((long)fileSize != reader.Length || (long)dataOffset > reader.Length)
            {
                return FileType.Unknown;
            }
            return FileType.SerializedFile;
        }
        return FileType.Unknown;
    }
}

public interface IVirtualFile : IStreamProvider
{
    public string Path { get; }
    public string Name { get; }
    public FileType FileType { get; }
}