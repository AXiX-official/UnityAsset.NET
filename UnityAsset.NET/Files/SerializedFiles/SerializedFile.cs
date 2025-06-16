using System.Text;
using UnityAsset.NET.IO;

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
    
    public static SerializedFile Parse(DataBuffer db)
    {
        var header = SerializedFileHeader.Parse(db);
        db.IsBigEndian = header.Endianess;
        var metadata = SerializedFileMetadata.Parse(db, header.Version);
        var assets = new List<Asset>();
        for (int i = 0; i < metadata.AssetInfos.Count; i++)
        {
            var assetInfo = metadata.AssetInfos[i];
            assets.Add(new Asset(assetInfo, new DataBuffer(db.Slice((int)(header.DataOffset + assetInfo.ByteOffset), (int)assetInfo.ByteSize).ToArray(), header.Endianess)));
        }
        return new SerializedFile(header, metadata, assets);
    }

    public void Serialize(DataBuffer db)
    {
        db.EnsureCapacity((int)SerializeSize);
        Header.Serialize(db);
        db.IsBigEndian = Header.Endianess;
        Metadata.Serialize(db, Header.Version);
        db.Seek((int)Header.DataOffset);
        foreach (var asset in Assets)
        {
            db.Seek((int)(Header.DataOffset + asset.Info.ByteOffset));
            db.WriteBytes(asset.RawData.AsSpan());
        }
    }
    
    public void Serialize(string path)
    {
        DataBuffer db = new DataBuffer(0);
        Serialize(db);
        db.WriteToFile(path);
    }

    public long SerializeSize
    {
        get {
            var lastAssetInfo = Metadata.AssetInfos.MaxBy(info => info.ByteOffset);
            return (long)(Header.DataOffset + lastAssetInfo.ByteOffset + lastAssetInfo.ByteSize);
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Header.ToString());
        sb.AppendLine(Metadata.ToString());
        return sb.ToString();
    }
}