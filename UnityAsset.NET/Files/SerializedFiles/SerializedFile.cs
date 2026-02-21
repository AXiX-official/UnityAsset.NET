using System.Text;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedFile : IFile
{
    public readonly SerializedFileHeader Header;
    public readonly SerializedFileMetadata Metadata;
    public List<Asset> Assets = new();
    public readonly Dictionary<Int64, string> Containers = new();
    public BundleFile? ParentBundle { get; private set; }
    public IVirtualFileInfo? SourceVirtualFileInfo { get; private set; }
    public readonly Dictionary<Int64, Asset> PathToAsset = new();
    public IReaderProvider ReaderProvider { get; }
    
    public SerializedFile(SerializedFileHeader header, SerializedFileMetadata metadata, List<Asset> assets, IReaderProvider readerProvider, BundleFile? parentBundle)
    {
        Header = header;
        Metadata = metadata;
        if (string.IsNullOrEmpty(Metadata.UnityVersion))
            Metadata.UnityVersion = Setting.DefaultUnityVerion;
        Assets = assets;
        ReaderProvider = readerProvider;
        ParentBundle = parentBundle;
        SourceVirtualFileInfo = parentBundle?.SourceVirtualFile;
        Process();
    }

    public SerializedFile(IVirtualFileInfo fileInfo)
    {
        ReaderProvider = new CustomFileReaderProvider(fileInfo);
        var reader = ReaderProvider.CreateReader();
        Header = SerializedFileHeader.Parse(reader);
        reader.Endian = Header.Endianness;
        Metadata = SerializedFileMetadata.Parse(reader, Header.Version);
        if (string.IsNullOrEmpty(Metadata.UnityVersion))
            Metadata.UnityVersion = Setting.DefaultUnityVerion;
        foreach (var assetInfo in Metadata.AssetInfos.AsSpan())
        {
            Assets.Add(new Asset(this, assetInfo));
        }
        
        SourceVirtualFileInfo = fileInfo;
        Process();
    }
    
    public static SerializedFile Parse(BundleFile bf, IReaderProvider readerProvider)
    {
        var reader = readerProvider.CreateReader();
        var header = SerializedFileHeader.Parse(reader);
        reader.Endian = header.Endianness;
        var metadata = SerializedFileMetadata.Parse(reader, header.Version);
        if (metadata.UnityVersion == "0.0.0")
        {
            metadata.UnityVersion = Setting.DefaultUnityVerion;
        }
        var assets = new List<Asset>();
        var sf = new SerializedFile(header, metadata, assets, readerProvider, bf);
        foreach (var assetInfo in metadata.AssetInfos.AsSpan())
        {
            assets.Add(new Asset(sf, assetInfo));
        }
        sf.Process();

        if (readerProvider is SlicedReaderProvider srp)
        {
            if (srp.BaseReaderProvider is BlockReaderProvider brp)
            {
                BlockReader.RegisterAssetToBlockMap(srp, brp, sf);
            }
        }
        
        return sf;
    }

    public void Process()
    {
        foreach (var asset in Assets)
        {
            PathToAsset.Add(asset.PathId, asset);
        }
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