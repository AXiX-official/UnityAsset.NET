using System.Text;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedFile : IFile
{
    public SerializedFileHeader Header;
    public SerializedFileMetadata Metadata;
    public List<Asset> Assets;
    
    public SerializedFile(SerializedFileHeader header, SerializedFileMetadata metadata, List<Asset> assets)
    {
        Header = header;
        Metadata = metadata;
        if (string.IsNullOrEmpty(Metadata.UnityVersion))
            Metadata.UnityVersion = Setting.DefaultUnityVerion;
        Assets = assets;
    }
    
    public static SerializedFile Parse(IReader reader)
    {
        reader.Seek(0);
        var header = SerializedFileHeader.Parse(reader);
        reader.Endian = header.Endianness;
        var metadata = SerializedFileMetadata.Parse(reader, header.Version);
        if (metadata.UnityVersion == "0.0.0")
        {
            metadata.UnityVersion = Setting.DefaultUnityVerion;
        }
        var assets = new List<Asset>();
        foreach (var assetInfo in metadata.AssetInfos.AsSpan())
        {
            reader.Seek((int)(header.DataOffset + assetInfo.ByteOffset));
            assets.Add(new Asset(assetInfo, new SlicedReader(reader, (long)(header.DataOffset + assetInfo.ByteOffset),assetInfo.ByteSize)));
        }
        return new SerializedFile(header, metadata, assets);
    }

    /*public void Serialize(IWriter writer)
    {
        if (writer is MemoryBinaryIO mbio)
            mbio.EnsureCapacity((int)SerializeSize);
        Header.Serialize(writer);
        writer.Endian = Header.Endianness;
        Metadata.Serialize(writer, Header.Version);
        writer.Seek((int)Header.DataOffset);
        var assetsSpan = Assets.AsSpan();
        assetsSpan.Sort((a, b) => a.Info.ByteOffset.CompareTo(b.Info.ByteOffset));
        foreach (var asset in assetsSpan)
        {
            writer.Seek((int)(Header.DataOffset + asset.Info.ByteOffset));
            asset.RawData.Position = 0;
            writer.WriteBytes(asset.RawData.ReadBytes((int)asset.RawData.Length));
        }
    }
    
    public void Serialize(string path)
    {
        using FileStreamWriter fsw = new FileStreamWriter(path);
        Serialize(fsw);
    }

    public long SerializeSize
    {
        get {
            var lastAssetInfo = Metadata.AssetInfos.MaxBy(info => info.ByteOffset);
            return (long)(Header.DataOffset + lastAssetInfo.ByteOffset + lastAssetInfo.ByteSize);
        }
    }*/

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Header.ToString());
        sb.AppendLine(Metadata.ToString());
        return sb.ToString();
    }
}