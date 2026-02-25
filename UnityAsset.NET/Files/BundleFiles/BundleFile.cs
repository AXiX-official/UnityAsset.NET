using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;

namespace UnityAsset.NET.Files;

public class BundleFile : IFile
{
    /// <summary>
    /// BundleFile's Header
    /// </summary>
    public readonly Header? Header;
    /// <summary>
    /// Blocks and Cab info
    /// </summary>
    public readonly BlocksAndDirectoryInfo? DataInfo;
    /// <summary>
    /// SerializedFiles and BinaryData
    /// </summary>
    public readonly List<FileWrapper> Files;
    /// <summary>
    /// Optional UnityCN encryption data
    /// </summary>
    public readonly UnityCN? UnityCnInfo;
    /// <summary>
    /// Optional key for UnityCN encryption
    /// </summary>
    public readonly string? UnityCnKey;

    public uint? Crc32;
    
    public IVirtualFileInfo? SourceVirtualFile { get; private set; }

    public static UnityCN? ParseUnityCnInfo(IReader reader, Header header, string? key)
    {
        var unityCnMask = UnityCNVersionJudge(header.UnityRevision) 
            ? ArchiveFlags.BlockInfoNeedPaddingAtStart 
            : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
        
        if ((header.Flags & unityCnMask) != 0)
        {
            key ??= Setting.DefaultUnityCNKey;
            if (key == null) throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {unityCnMask}");
            return new UnityCN(reader, key);
        }
        return null;
    }

    public static void AlignAfterHeader(IReader reader, Header header)
    {
        if (header.Version >= 7 || header.UnityRevision is {Major: 2019, Minor: 4, Patch: >= 15})
        {
            reader.Align(16);
        }
    }

    public static BlocksAndDirectoryInfo ParseDataInfo(IReader reader, Header header)
    {
        byte[] blocksInfoBytes;
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0)
        {
            var pos = reader.Position;
            reader.Seek((int)(header.Size - header.CompressedBlocksInfoSize));
            blocksInfoBytes = reader.ReadBytes((int)header.CompressedBlocksInfoSize);
            reader.Seek(pos);
        }
        else
        {
            blocksInfoBytes = reader.ReadBytes((int)header.CompressedBlocksInfoSize);
        }
        var compressionType = (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask);
        MemoryReader blocksInfoUncompressedData = new MemoryReader((int)header.UncompressedBlocksInfoSize);
        Compression.DecompressToBytes(blocksInfoBytes, blocksInfoUncompressedData.AsWritableSpan, compressionType);
        var dataInfo = BlocksAndDirectoryInfo.Parse(blocksInfoUncompressedData);
        if (dataInfo.BlocksInfo.Any(blockInfo => blockInfo.UncompressedSize > int.MaxValue))
        {
            throw new Exception("Block size too large.");
        }
        if (!UnityCNVersionJudge(header.UnityRevision) && (header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            reader.Align(16);
        return dataInfo;
    }

    public BundleFile(Header header, BlocksAndDirectoryInfo dataInfo, List<FileWrapper> files, string? key = null)
    {
        UnityCnKey = key;
        Header = header;
        DataInfo = dataInfo;
        Files = files;
    }

    /// <summary>
    /// Parses only the BundleFile container structure without further parsing the contained SerializedFile format.
    /// </summary>
    /// <remarks>
    /// This method stops at the BundleFile level and does not process the internal SerializedFile structures.
    /// If you need full parsing including SerializedFile format conversion, use <see cref="BundleFileExtensions.ParseFilesWithTypeConversion"/> instead.
    /// </remarks>
    /// <param name="path">The file path to the BundleFile</param>
    /// <param name="key">Optional decryption key</param>
    /// <param name="lazyLoad"></param>
    public BundleFile(string path, string? key = null, bool lazyLoad = true)
        : this(new DirectFileInfo(path), key, lazyLoad) {}
    
    public BundleFile(IVirtualFileInfo fileInfo, string? key = null, bool lazyLoad = true)
    {
        var cfrProvider = new CustomFileReaderProvider(fileInfo);
        var reader = cfrProvider.CreateReader();
        UnityCnKey = key;
        Header = Header.Parse(reader);
        if (Header.UnityRevision == "0.0.0")
        {
            Header.UnityRevision = Setting.DefaultUnityVerion;
        }
        UnityCnInfo = ParseUnityCnInfo(reader, Header, UnityCnKey);
        UnityCnKey = UnityCnInfo?.Key;
        AlignAfterHeader(reader, Header);
        DataInfo = ParseDataInfo(reader, Header);
        
        var blockReaderProvider = new BlockReaderProvider(DataInfo.BlocksInfo, new SliceFile(fileInfo.GetFile(), (ulong)reader.Position,
            (ulong)(reader.Length - reader.Position)), UnityCnInfo);
        Files = new List<FileWrapper>();
        foreach (var dir in DataInfo.DirectoryInfo)
        {
            Files.Add(new FileWrapper(new SlicedReaderProvider(blockReaderProvider, dir.Offset, dir.Size), dir));
        }

        if (!lazyLoad)
        {
            ParseFilesWithTypeConversion();
        }

        SourceVirtualFile = fileInfo;
    }

    public void ParseFilesWithTypeConversion()
    {
        for (int i = 0; i < Files.Count; i++)
        {
            var subFile = Files[i];
            if (subFile is { CanBeSerializedFile: true, File: IReaderProvider provider})
            {
                Files[i] = new FileWrapper(SerializedFile.Parse(this, provider), subFile.Info);
            }
        }
    }

    public void Serialize(string path, CompressionType infoPacker = CompressionType.Lz4HC,
        CompressionType dataPacker = CompressionType.Lz4HC, string? unityCnKey = null)
    {
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Serialize(output, false, infoPacker, dataPacker, unityCnKey);
    }
    
    public void Serialize(Stream output, bool leaveOpen = false, CompressionType infoPacker = CompressionType.Lz4HC, CompressionType dataPacker = CompressionType.Lz4HC, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || Files == null)
            throw new NullReferenceException("BundleFile has not read completely.");
        
        if (unityCnKey is not null && (dataPacker is not CompressionType.Lz4 && dataPacker is not CompressionType.Lz4HC))
            throw new Exception($"UnityCN Encryption only support packing data with Lz4/Lz4HC, but {dataPacker} was set.");
        
        using var blockWriter = BlockStreamWriter.GetBlockWriter(dataPacker);
        
        var directoryInfo = new List<FileEntry>();
        ulong offset = 0;
        foreach (var file in Files.AsReadOnlySpan())
        {
            ulong cabSize;
            switch (file.File)
            {
                case IReaderProvider rp:
                    var reader = rp.CreateReader();
                    cabSize = blockWriter.WriteBytes(reader);
                    break;
                case SerializedFile sf:
                    throw new NotImplementedException();
                    /*MemoryBinaryIO sfBuffer = new MemoryBinaryIO((int)sf.SerializeSize);
                    sf.Serialize(sfBuffer);
                    cabSize = (int)sfBuffer.Position;
                    writer.WriteBytes(sfBuffer.ReadOnlySlice(0, cabSize));
                    break;*/
                default:
                    throw new Exception($"Unexpected type: {file.File.GetType().Name}");
            }
            directoryInfo.Add(new FileEntry(offset, cabSize, file.Info.Flags, file.Info.Path));
            offset += cabSize;
        }
        blockWriter.Finish();
        using var blockStream = blockWriter.GetDataStream();
        
        var dataInfo = new BlocksAndDirectoryInfo(DataInfo.UncompressedDataHash, blockWriter.BlockInfos.ToArray(), directoryInfo.ToArray());
        using var dataInfoStream = new MemoryStream();
        var dataInfoWriter = new CustomStreamWriter(dataInfoStream);
        dataInfo.Serialize(dataInfoWriter);
        dataInfoWriter.Finish();
        var uncompressedBlocksInfoSize = dataInfoStream.Length;
        var compressedDataInfoStream = Compression.CompressToStream(dataInfoStream.ToArray(), infoPacker);
        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision.ToString(),
            0, (uint)compressedDataInfoStream.Length, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker);
        if (unityCnKey == null && UnityCnKey != null)
        {
            var unityCnMask = UnityCNVersionJudge(header.UnityRevision) 
                ? ArchiveFlags.BlockInfoNeedPaddingAtStart 
                : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
            if (unityCnMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
                unityCnMask = (Header.Flags & ArchiveFlags.UnityCNEncryptionNew) != 0 ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            header.Flags &= ~unityCnMask;
        }

        bool needAlignAfterHeader = false;
        bool needAlignAfterInfo = false;
        bool infoAtEnd = (header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0;
        if (!UnityCNVersionJudge(header.UnityRevision) &&
            (header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            needAlignAfterInfo = true;
        if (Header.Version >= 7
            || Header.UnityRevision is { Major: 2019, Minor: 4, Patch: >= 15 }
            || !UnityCNVersionJudge(Header.UnityRevision) &&
            (Header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            needAlignAfterHeader = true;

        header.Size = header.SerializeSize;
        if (unityCnKey != null && UnityCnInfo != null)
            header.Size += UnityCnInfo.SerializeSize;
        if (needAlignAfterHeader)
            header.Size = Align(header.Size, 16);
        if (!infoAtEnd)
        {
            header.Size += compressedDataInfoStream.Length;
            if (needAlignAfterInfo) header.Size = Align(header.Size, 16);
        }
        header.Size += blockStream.Length;
        if (infoAtEnd)
        {
            header.Size += compressedDataInfoStream.Length;
            if (needAlignAfterInfo) header.Size = Align(header.Size, 16);
        }
        
        var writer = new CustomStreamWriter(output, leaveOpen: leaveOpen);
        header.Serialize(writer);
        if (unityCnKey != null && UnityCnInfo != null)
            UnityCnInfo.Serialize(writer);
        if (needAlignAfterHeader)
            ((IWriter)writer).Align(16);
        if (!infoAtEnd)
        {
            writer.WriteStream(compressedDataInfoStream);
            if (needAlignAfterInfo) ((IWriter)writer).Align(16);
        }
        writer.WriteStream(blockStream);
        if (infoAtEnd)
        {
            writer.WriteStream(compressedDataInfoStream);
            if (needAlignAfterInfo) ((IWriter)writer).Align(16);
        }
    }

    private static long Align(long size, int alignment)
    {
        var offset = size % alignment;
        if (offset == 0)
            return size;
        return size + alignment - offset;
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Header: {Header}");
        sb.AppendLine($"DataInfo: {DataInfo}");
        var filesSpan = Files.AsSpan();
        for (int i = 0; i  < filesSpan.Length; ++i)
            sb.AppendLine($"File {i}: {filesSpan[i]}"); 
        return sb.ToString();
    }

    public static bool UnityCNVersionJudge(UnityRevision version)
    {
        return version < "2020" || //2020 and earlier
               (version >= "2020.3" && version <= "2020.3.34") || //2020.3.34 and earlier
               (version >= "2021.3" && version <= "2021.3.2") || //2021.3.2 and earlier
               (version >= "2022.3" && version <= "2022.3.1"); //2022.3.1 and earlier
    }
}