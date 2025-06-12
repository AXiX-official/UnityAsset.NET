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
    
    public static SerializedFile Parse(ref DataBuffer db)
    {
        var header = SerializedFileHeader.Parse(ref db);
        db.IsBigEndian = header.Endianess;
        var metadata = SerializedFileMetadata.Parse(ref db, header.Version);
        for (int i = 0; i < metadata.AssetInfos.Count; i++)
        {
            var assetInfo = metadata.AssetInfos[i];
            assetInfo.Data =
                db.AsSpan().Slice((int)(header.DataOffset + assetInfo.ByteOffset), (int)assetInfo.ByteSize).ToArray();
        }
        return new SerializedFile(header, metadata);
    }

    public void Serialize(ref DataBuffer db)
    {
        db.EnsureCapacity((int)SerializeSize);
        Header.Serialize(ref db);
        db.IsBigEndian = Header.Endianess;
        Metadata.Serialize(ref db, Header.Version);
        db.Seek((int)Header.DataOffset);
        foreach (var assetInfo in Metadata.AssetInfos)
        {
            db.Seek((int)(Header.DataOffset + assetInfo.ByteOffset));
            db.WriteBytes(assetInfo.Data);
        }
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
        sb.AppendLine("Serialized File:");
        sb.AppendLine(Header.ToString());
        sb.AppendLine(Metadata.ToString());
        return sb.ToString();
    }
}