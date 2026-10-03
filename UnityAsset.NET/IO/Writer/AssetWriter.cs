using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.IO.Writer;

public class AssetWriter : CustomStreamWriter
{
    public readonly SerializedFile AssetsFile;

    public AssetWriter(Stream stream, SerializedFile assetsFile, Endianness? endian = null,
        bool leaveOpen = false, int bufferSize = 8192) : base(stream, endian ?? assetsFile.Header.Endianness, leaveOpen, bufferSize)
    {
        AssetsFile = assetsFile;
    }
}
