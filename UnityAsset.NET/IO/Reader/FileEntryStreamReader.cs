using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.IO.Stream;

namespace UnityAsset.NET.IO.Reader;

public class FileEntryStreamReader : CustomStreamReader
{
    public FileEntryStreamReader(BlockStream blockStream, FileEntry fileEntry, Endianness endian = Endianness.BigEndian, FileShare fileShare = FileShare.Read)
        : base(new FileEntryStream(blockStream, fileEntry), endian, false)
    {
    }
}