using System.Text;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;

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

    public SerializedFile(SerializedFileHeader header, SerializedFileMetadata metadata, List<Asset> assets, IReaderProvider readerProvider, BundleFile? parentBundle, string? defaultUnityVersion = null)
    {
        Header = header;
        Metadata = metadata;
        if (string.IsNullOrEmpty(Metadata.UnityVersion))
            Metadata.UnityVersion = defaultUnityVersion ?? parentBundle?.DefaultUnityVersion ?? Setting.DefaultUnityVerion;
        Assets = assets;
        ReaderProvider = readerProvider;
        ParentBundle = parentBundle;
        SourceVirtualFileInfo = parentBundle?.SourceVirtualFile;
        Process();
    }

    public SerializedFile(IVirtualFileInfo fileInfo, string? defaultUnityVersion = null)
    {
        ReaderProvider = new CustomFileReaderProvider(fileInfo);
        var reader = ReaderProvider.CreateReader();
        Header = SerializedFileHeader.Parse(reader);
        reader.Endian = Header.Endianness;
        Metadata = SerializedFileMetadata.Parse(reader, Header.Version);
        if (string.IsNullOrEmpty(Metadata.UnityVersion))
            Metadata.UnityVersion = defaultUnityVersion ?? Setting.DefaultUnityVerion;
        foreach (var assetInfo in Metadata.AssetInfos.AsSpan())
        {
            Assets.Add(new Asset(this, assetInfo));
        }

        SourceVirtualFileInfo = fileInfo;
        Process();
    }

    public static SerializedFile Parse(BundleFile bf, IReaderProvider readerProvider, string? defaultUnityVersion = null)
    {
        var reader = readerProvider.CreateReader();
        var header = SerializedFileHeader.Parse(reader);
        reader.Endian = header.Endianness;
        var metadata = SerializedFileMetadata.Parse(reader, header.Version);
        if (metadata.UnityVersion == "0.0.0")
        {
            metadata.UnityVersion = defaultUnityVersion ?? bf.DefaultUnityVersion ?? Setting.DefaultUnityVerion;
        }
        var assets = new List<Asset>();
        var sf = new SerializedFile(header, metadata, assets, readerProvider, bf);
        foreach (var assetInfo in metadata.AssetInfos.AsSpan())
        {
            assets.Add(new Asset(sf, assetInfo));
        }
        sf.Process();

        return sf;
    }

    public void Process()
    {
        foreach (var asset in Assets)
        {
            PathToAsset.Add(asset.PathId, asset);
        }
    }

    internal void Write(IWriter writer, List<IAsset> assets)
    {
        var start = writer.Position;

        var assetsSorted = new List<IAsset>(assets);
        assetsSorted.Sort((a, b) => a.PathId.CompareTo(b.PathId));

        EnsureUniquePathIds(assetsSorted);

        // The type table is kept as it was in the file: entries that no asset uses still belong to it, and
        // TypeDependencies (v21+) are indices into it. Only types the file does not carry yet are appended.
        var types = new List<SerializedType>(Metadata.Types);
        var typeIndexByReference = new Dictionary<SerializedType, int>(ReferenceEqualityComparer.Instance);
        var typeIndexByIdentity = new Dictionary<(int TypeId, short ScriptTypeIndex), int>();
        for (var i = 0; i < types.Count; i++)
        {
            typeIndexByReference[types[i]] = i;
            typeIndexByIdentity.TryAdd((types[i].TypeID, types[i].ScriptTypeIndex), i);
        }

        // A type that is not in the table yet (a new asset type) is appended; existing entries are never
        // reordered, because TypeDependencies and the asset table address them by index.
        int IndexOf(SerializedType type)
        {
            if (typeIndexByReference.TryGetValue(type, out var index))
                return index;

            var identity = (type.TypeID, type.ScriptTypeIndex);
            if (typeIndexByIdentity.TryGetValue(identity, out index))
            {
                typeIndexByReference[type] = index;
                return index;
            }

            index = types.Count;
            types.Add(type);
            typeIndexByReference[type] = index;
            typeIndexByIdentity[identity] = index;
            return index;
        }
        
        ulong Align8(ulong value) => (value + 7) & ~7UL;
        ulong Align16(ulong value) => (value + 15) & ~15UL;

        var assetFileInfos = new List<AssetFileInfo>(assetsSorted.Count);
        ulong bodySize = 0;
        for (var i = 0; i < assetsSorted.Count; i++)
        {
            var asset = assetsSorted[i];
            if (i > 0)
                bodySize = Align8(bodySize);

            var size = checked((uint)asset.Size);
            assetFileInfos.Add(new AssetFileInfo(asset.PathId, bodySize, size, IndexOf(asset.Type), asset.Type));
            bodySize += size;
        }

        var metadata = new SerializedFileMetadata(Metadata.UnityVersion, Metadata.TargetPlatform,
            Metadata.TypeTreeEnabled, types.ToArray(), assetFileInfos.ToArray(), Metadata.ScriptTypes,
            Metadata.Externals, Metadata.RefTypes, Metadata.UserInformation);

        var metadataSize = CountWriter.Measure(w => metadata.Serialize(w, Header.Version));
        var headerSize = CountWriter.Measure(w => Header.Serialize(w));
        var dataOffset = Align16((ulong)(headerSize + metadataSize));

        var header = new SerializedFileHeader((uint)metadataSize, dataOffset + bodySize, Header.Version, dataOffset,
            Header.Endianness, Header.Reserved, Header.Unknown);

        header.Serialize(writer);
        writer.Endian = header.Endianness;
        metadata.Serialize(writer, header.Version);

        // Asset-level writes carry the file being written, because some members (streaming info, sprite atlas data)
        // serialize differently per Unity version. Binding to `this` rather than trusting the caller's writer keeps
        // that version correct even when the writer was built for another file.
        var assetWriter = new AssetWriterProxy(writer, this);

        void PadTo(IWriter w, long s, ulong relative)
        {
            var target = s + checked((long)relative);
            if (writer.Position > target)
                throw new InvalidOperationException(
                    $"Writing at position {w.Position} would overlap a section that ends at {target}.");

            if (w.Position < target)
                w.WriteBytes(0, (ulong)(target - w.Position));
        }

        for (var i = 0; i < assetsSorted.Count; i++)
        {
            PadTo(writer, start, dataOffset + assetFileInfos[i].ByteOffset);
            assetsSorted[i].Write(assetWriter);
        }

        PadTo(writer, start, dataOffset + bodySize);
    }

    private static void EnsureUniquePathIds(List<IAsset> assets)
    {
        if (assets.Count < 2)
            return;

        var counts = new Dictionary<long, int>(assets.Count);
        foreach (var asset in assets)
        {
            counts.TryGetValue(asset.PathId, out var count);
            counts[asset.PathId] = count + 1;
        }

        List<(long PathId, int Count)>? conflicts = null;
        foreach (var (pathId, count) in counts)
        {
            if (count > 1)
                (conflicts ??= []).Add((pathId, count));
        }

        if (conflicts is null)
            return;

        conflicts.Sort((a, b) => a.PathId.CompareTo(b.PathId));
        throw new InvalidOperationException(
            "Duplicate PathIDs: " + string.Join(", ", conflicts.Select(c => $"{c.PathId} (×{c.Count})")) + ".");
    }
    

    public void Serialize(IWriter writer)
    {
        Write(writer, Assets.Select(asset => (IAsset)asset).ToList());
        
        /*var pos = writer.Position;
        Header.Serialize(writer);
        writer.Endian = Header.Endianness;
        Metadata.Serialize(writer, Header.Version);
        var assets = new List<Asset>(Assets);
        assets.Sort((a, b) => a.Info.PathId.CompareTo(b.Info.PathId));
        foreach (var asset in assets)
        {
            var assetOffset = Header.DataOffset + asset.Info.ByteOffset;
            var target = (ulong)pos + assetOffset;
            // Unsigned distance, so an asset whose offset is already behind the cursor would underflow into an enormous
            // padding request instead of an error: writing 2^64 zero bytes is not a diagnosable failure, so it is
            // refused here.
            if (target < (ulong)writer.Position)
                throw new InvalidOperationException(
                    $"Asset {asset.PathId} is placed at offset {assetOffset}, which overlaps what has already been " +
                    $"written (position {writer.Position - pos}). The file's DataOffset is too small, or two assets overlap.");

            writer.WriteBytes(0, target - (ulong)writer.Position);
            writer.WriteBytes(asset.DataReader);
        }*/
    }

    public void Serialize(Stream output, bool leaveOpen = true)
    {
        using var writer = new CustomStreamWriter(output, leaveOpen: leaveOpen);
        Serialize(writer);
    }

    public void Serialize(string path)
    {
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Serialize(output, false);
    }

    public ulong WriteBytes(IWriter writer)
    {
        var pos = writer.Position;
        Serialize(writer);
        return (ulong)(writer.Position - pos);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Header.ToString());
        sb.AppendLine(Metadata.ToString());
        return sb.ToString();
    }
}