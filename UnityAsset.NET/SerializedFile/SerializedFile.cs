using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFile
{
    public SerializedFileHeader Header;
    
    public SerializedFile(Stream data)
    {
        using AssetReader reader = new AssetReader(data);
        Header = new SerializedFileHeader(reader);
        if (Header.Endianess == 0)
        {
            reader.BigEndian = false;
        }
    }
}