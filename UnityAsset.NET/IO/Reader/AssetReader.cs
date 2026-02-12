using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.IO.Reader;

public class AssetReader : SlicedReader
{
    public readonly SerializedFile AssetsFile;
    
    public AssetReader(IReaderProvider readerProvider, ulong start, ulong length, SerializedFile assetsFile, Endianness endian) : base(readerProvider, start, length)
    {
        AssetsFile = assetsFile;
        BaseReader.Endian = endian;
    }
}