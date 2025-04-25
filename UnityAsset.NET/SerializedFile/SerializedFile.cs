using System.Text;
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
        using AssetReader r = new AssetReader(data);
        Header = new SerializedFileHeader(r);
        r.BigEndian = Header.Endianess;
        Metadata = new SerializedFileMetadata(r, Header.Version);
    }
    
    public SerializedFile(byte[] data)
    {
        using AssetReader reader = new AssetReader(data);
        Header = new SerializedFileHeader(reader);
        reader.BigEndian = Header.Endianess;
        Metadata = new SerializedFileMetadata(reader, Header.Version);
    }
    
    public SerializedFile(string path)
    {
        using AssetReader reader = new AssetReader(path);
        Header = new SerializedFileHeader(reader);
        reader.BigEndian = Header.Endianess;
        Metadata = new SerializedFileMetadata(reader, Header.Version);
    }

    public void Write(AssetWriter w)
    {
        Header.Write(w);
        w.BigEndian = Header.Endianess;
        Metadata.Write(w, Header.Version);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized File:");
        sb.AppendLine(Header.ToString());
        sb.AppendLine(Metadata.ToString());
        return sb.ToString();
    }
}