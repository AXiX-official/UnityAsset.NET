using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.IO.Reader;

public class AssetReader : SlicedReader
{
    public SerializedFile AssetsFile;
    
    public AssetReader(IReader reader, long start, long length, SerializedFile assetsFile) : base(reader, start, length)
    {
        AssetsFile = assetsFile;
    }
}