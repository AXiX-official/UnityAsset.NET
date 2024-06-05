using System.Text.RegularExpressions;

using UnityAsset.NET.IO;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFile
{
    public SerializedFileHeader Header;
    
    public SerializedFileMetadata Metadata;
    
    public int[] version = { 0, 0, 0, 0 };
    
    public SerializedFile(Stream data)
    {
        using AssetReader reader = new AssetReader(data);
        Header = new SerializedFileHeader(reader);
        Metadata = new SerializedFileMetadata(reader, Header.Version);
    }
}