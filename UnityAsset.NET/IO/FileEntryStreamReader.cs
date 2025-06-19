using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO;

public class FileEntryStreamReader : StreamReader
{
    public FileEntryStreamReader(BlockStream blockStream, FileEntry fileEntry, Endianness endian = Endianness.BigEndian, FileShare fileShare = FileShare.Read)
        : base(new FileEntryStream(blockStream, fileEntry), endian, false)
    {
    }
}