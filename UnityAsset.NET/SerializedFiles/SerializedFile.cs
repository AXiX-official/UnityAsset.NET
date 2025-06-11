using System.Text;

using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public sealed class SerializedFile : ICabFile
{
    public SerializedFileHeader Header;
    public SerializedFileMetadata Metadata;
    
    public SerializedFile(SerializedFileHeader header, SerializedFileMetadata metadata)
    {
        Header = header;
        Metadata = metadata;
    }
    
    public static SerializedFile ParseFromReader(AssetReader reader)
    {
        var header = SerializedFileHeader.ParseFromReader(reader);
        reader.BigEndian = header.Endianess;
        var metadata = SerializedFileMetadata.ParseFromReader(reader, header.Version);
        foreach (var assetInfo in metadata.AssetInfos)
        {
            byte[] buffer = new byte[assetInfo.ByteSize];
            reader.Position = (long)(header.DataOffset + assetInfo.ByteOffset);
            reader.BaseStream.ReadExactly(buffer, 0, (int)assetInfo.ByteSize);
            assetInfo.DataReader = new AssetReader(buffer);
        }
        return new SerializedFile(header, metadata);
    }

    public void Serialize(AssetWriter writer)
    {
        var p = writer.Position;
        Header.Serialize(writer);
        writer.BigEndian = Header.Endianess;
        Metadata.Serialize(writer, Header.Version);
        var lastAssetInfo = Metadata.AssetInfos.MaxBy(info => info.ByteOffset);
        writer.Position = p + (long)(Header.DataOffset + lastAssetInfo.ByteOffset + lastAssetInfo.ByteSize);
        writer.Position = p + (long)Header.DataOffset;
        foreach (var assetInfo in Metadata.AssetInfos)
        {
            writer.Position = p + (long)(Header.DataOffset + assetInfo.ByteOffset);
            assetInfo.DataReader.Position = 0;
            assetInfo.DataReader.BaseStream.CopyTo(writer.BaseStream);
        }
        writer.Position = p + (long)(Header.DataOffset + lastAssetInfo.ByteOffset + lastAssetInfo.ByteSize);
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