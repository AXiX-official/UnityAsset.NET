using System.Runtime.InteropServices;
using System.Text;
using UnityAsset.NET.Extensions;
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
        foreach (var assetInfo in metadata.AssetInfos.AsSpan())
            assets.Add(new Asset(assetInfo, new DataBuffer(db.Slice((int)(header.DataOffset + assetInfo.ByteOffset), (int)assetInfo.ByteSize).ToArray(), header.Endianess)));
        return new SerializedFile(header, metadata, assets);
    }

    public int Serialize(DataBuffer db)
    {
        var pos = db.Position;
        db.EnsureCapacity((int)SerializeSize);
        Header.Serialize(db);
        db.IsBigEndian = Header.Endianess;
        Metadata.Serialize(db, Header.Version);
        db.Seek((int)Header.DataOffset);
        var assetsSpan = Assets.AsSpan();
        assetsSpan.Sort((a, b) => a.Info.ByteOffset.CompareTo(b.Info.ByteOffset));
        foreach (var asset in assetsSpan)
        {
            db.Seek((int)(Header.DataOffset + asset.Info.ByteOffset));
            db.WriteBytes(asset.RawData.AsSpan());
        }
        return db.Position - pos;
    }
    
    public int Serialize(string path)
    {
        DataBuffer db = new DataBuffer(0);
        int size = Serialize(db);
        db.WriteToFile(path, size);
        return size;
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