using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Stream;

namespace UnityAsset.NET.Files;

public class BundleFile : IFile
{
    /// <summary>
    /// BundleFile's Header
    /// </summary>
    public Header? Header;
    /// <summary>
    /// Blocks and Cab info
    /// </summary>
    public BlocksAndDirectoryInfo? DataInfo;
    /// <summary>
    /// SerializedFiles and BinaryData
    /// </summary>
    public List<FileWrapper> Files;
    /// <summary>
    /// Optional UnityCN encryption data
    /// </summary>
    public UnityCN? UnityCnInfo;
    /// <summary>
    /// Optional key for UnityCN encryption
    /// </summary>
    public string? UnityCnKey;

    public uint? Crc32;
    
    public IVirtualFile? SourceVirtualFile { get; private set; }

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
        if (header.Version >= 7)
        {
            reader.Align(16);
        } else { // temp fix for 2019.4.x
            var version = header.UnityRevision;
            if (version.Major == 2019 && version.Minor == 4 && version.Patch >= 30)
            {
                reader.Align(16);
            }
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
    
    // crc calculate disabled
    public static (List<FileWrapper>, uint?) LazyParseFiles(SlicedReaderProvider readerProvider, BlocksAndDirectoryInfo dataInfo, UnityCN? unityCnInfo = null)
    {
        var blockStreamProvider = new BlockStreamProvider(dataInfo.BlocksInfo, readerProvider, unityCnInfo);
        var files = new List<FileWrapper>();
        foreach (var dir in dataInfo.DirectoryInfo.AsReadOnlySpan())
        {
            files.Add(new FileWrapper(new CustomStreamReaderProvider(new FileEntryStreamProvider(blockStreamProvider, dir)), dir));
        }

        return (files, null);
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
        : this(new FileStreamProvider(path), key, lazyLoad) {}
    
    public BundleFile(IStreamProvider streamProvider, string? key = null, bool lazyLoad = true)
    {
        var csProvider = new CustomStreamReaderProvider(streamProvider);
        using var reader = csProvider.CreateReader();
        UnityCnKey = key;
        Header = Header.Parse(reader);
        if (Header.UnityRevision == "0.0.0")
        {
            Header.UnityRevision = Setting.DefaultUnityVerion;
        }
        UnityCnInfo = ParseUnityCnInfo(reader, Header, UnityCnKey);
        AlignAfterHeader(reader, Header);
        DataInfo = ParseDataInfo(reader, Header);
        
        var readerProvider = new SlicedReaderProvider(csProvider, (ulong)reader.Position,
            (ulong)(reader.Length - reader.Position));
        (Files, Crc32) = LazyParseFiles(readerProvider, DataInfo, UnityCnInfo);

        if (!lazyLoad)
        {
            ParseFilesWithTypeConversion();
        }

        if (streamProvider is IVirtualFile file)
            SourceVirtualFile = file;
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
    
    /*public void Serialize(MemoryBinaryIO writer, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || Files == null)
            throw new NullReferenceException("BundleFile has not read completely.");
        var directoryInfo = new List<FileEntry>();
        var filesCapacity = Files.Sum(c =>
        {
            if (c.File is IReader r) return r.Length;
            if (c.File is SerializedFile sf) return sf.SerializeSize;
            return 0;
        });
        MemoryBinaryIO filesBuffer = new MemoryBinaryIO((int)filesCapacity);
        Int64 offset = 0;
        foreach (var file in Files.AsReadOnlySpan())
        {
            int cabSize;
            switch (file.File)
            {
                case IReader r:
                    r.Position = 0;
                    filesBuffer.WriteBytes(r.ReadBytes((int)r.Length));
                    cabSize = (int)r.Length;
                    break;
                case SerializedFile sf:
                    var pos = filesBuffer.Position;
                    sf.Serialize(filesBuffer);
                    cabSize = (int)(filesBuffer.Position - pos);
                    break;
                default:
                    throw new Exception($"Unexpected type: {file.File.GetType().Name}");
            }
            directoryInfo.Add(new FileEntry(offset, cabSize, file.Info.Flags, file.Info.Path));
            offset += cabSize;
        }
        filesBuffer.Seek(0);

        var blocksInfo = new List<StorageBlockInfo>();
        var blocksSize = offset;
        MemoryBinaryIO compressedBlocksDataBuffer = new MemoryBinaryIO((int)blocksSize);
        var defaultChunkSize = dataPacker == CompressionType.Lzma ? Int32.MaxValue : Setting.DefaultChunkSize;
        int compressedBlocksOffset = 0;
        while (blocksSize > 0)
        {
            int chunkSize = (int)Math.Min(blocksSize, defaultChunkSize);
            var compressedSize = Compression.CompressToBytes(
                filesBuffer.ReadOnlySlice(compressedBlocksOffset, chunkSize),
                compressedBlocksDataBuffer.SliceForward(),
                dataPacker);
            blocksInfo.Add(new StorageBlockInfo((UInt32)chunkSize, (UInt32)compressedSize, (StorageBlockFlags)dataPacker));
            blocksSize -= chunkSize;
            compressedBlocksOffset += chunkSize;
            ((ISeek)compressedBlocksDataBuffer).Advance((int)compressedSize);
        }
        int compressedBlocksDateSize = (int)compressedBlocksDataBuffer.Position;
        compressedBlocksDataBuffer.Seek(0);

        UnityCN? unityCnInfo = null;
        var version = ParseVersion(Header);
        if (unityCnKey != null)
        {
            unityCnInfo = new UnityCN(unityCnKey);
            if (dataPacker == CompressionType.Lz4 || dataPacker == CompressionType.Lz4HC)
            {
                var blocksInfoSpan = blocksInfo.AsSpan();
                for (int i = 0; i < blocksInfoSpan.Length; i++)
                {
                    var blockInfo = blocksInfoSpan[i];
                    blockInfo.Flags |= (StorageBlockFlags)0x100;
                    unityCnInfo.EncryptBlock(compressedBlocksDataBuffer.GetWritableSpan((int)blockInfo.CompressedSize), (int)blockInfo.CompressedSize, i);
                }
                compressedBlocksDataBuffer.Seek(0);
            }
            else
                throw new Exception($"UnityCN Encryption only support Lz4/Lz4HC, but {dataPacker} was set.");
        }
        else if (UnityCnKey != null)
        {
            var unityCnMask = VersionJudge1(version)
                ? ArchiveFlags.BlockInfoNeedPaddingAtStart
                : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
            if (unityCnMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
                unityCnMask = (Header.Flags & ArchiveFlags.UnityCNEncryptionNew) != 0 ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            Header.Flags &= ~unityCnMask;
        }

        var dataInfo = new BlocksAndDirectoryInfo(DataInfo.UncompressedDataHash, blocksInfo, directoryInfo);
        MemoryBinaryIO dataInfoBuffer = new MemoryBinaryIO((int)dataInfo.SerializeSize);
        dataInfo.Serialize(dataInfoBuffer);
        var uncompressedBlocksInfoSize = dataInfoBuffer.Length;
        MemoryBinaryIO compressedDataInfoBuffer = new MemoryBinaryIO((int)uncompressedBlocksInfoSize);
        var compressedBlocksInfoSize = Compression.CompressToBytes(dataInfoBuffer.AsReadOnlySpan, compressedDataInfoBuffer.GetWritableSpan((int)uncompressedBlocksInfoSize), infoPacker);
        compressedDataInfoBuffer.Position = 0;
        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision,
            0, (uint)compressedBlocksInfoSize, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker);
        writer.EnsureCapacity((int)(header.SerializeSize + 16 + compressedBlocksInfoSize + compressedBlocksDataBuffer.Length + unityCnInfo?.SerializeSize ?? 0));
        var position = writer.Position;
        header.Serialize(writer);
        if (unityCnInfo != null)
            unityCnInfo.Serialize(writer);
        if (header.Version >= 7)
            ((ISeek)writer).Align(16);
        else // temp fix for 2019.4.x
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                ((ISeek)writer).Align(16);
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0) //0x40 BlocksAndDirectoryInfoCombined
            writer.WriteBytes(compressedDataInfoBuffer.ReadOnlySlice(0, (int)compressedBlocksInfoSize));
        if (!VersionJudge1(version) &&(header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            ((ISeek)writer).Align(16);
        writer.WriteBytes(compressedBlocksDataBuffer.ReadOnlySlice(0, compressedBlocksDateSize));
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            writer.WriteBytes(compressedDataInfoBuffer.ReadOnlySlice(0, (int)compressedBlocksInfoSize));
        header.Size = writer.Position - position;
        writer.Size = (int)header.Size;
        writer.Seek(0);
        header.Serialize(writer);
    }
    
    public void StreamSerialize(FileStreamWriter writer, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || Files == null)
            throw new NullReferenceException("BundleFile has not read completely.");
        Header.Serialize(writer);
        var version = ParseVersion(Header);
        if (unityCnKey != null && UnityCnInfo != null)
            UnityCnInfo.Serialize(writer);
        if (Header.Version >= 7)
            writer.Align(16);
        else // temp fix for 2019.4.x
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                writer.Align(16);
        if (!VersionJudge1(version) &&(Header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            writer.Align(16);
        writer.EnableCompression = true;
        writer.CompressionType = dataPacker;
        //writer.BufferSize = dataPacker == CompressionType.Lzma ? Int32.MaxValue - 9 : Setting.DefaultChunkSize;
        var directoryInfo = new List<FileEntry>();
        long offset = 0;
        foreach (var file in Files.AsReadOnlySpan())
        {
            int cabSize;
            switch (file.File)
            {
                case IReader r:
                    r.Position = 0;
                    writer.WriteBytes(r.ReadBytes((int)r.Length));
                    cabSize = (int)r.Length;
                    break;
                case SerializedFile sf:
                    MemoryBinaryIO sfBuffer = new MemoryBinaryIO((int)sf.SerializeSize);
                    sf.Serialize(sfBuffer);
                    cabSize = (int)sfBuffer.Position;
                    writer.WriteBytes(sfBuffer.ReadOnlySlice(0, cabSize));
                    break;
                default:
                    throw new Exception($"Unexpected type: {file.File.GetType().Name}");
            }
            directoryInfo.Add(new FileEntry(offset, cabSize, file.Info.Flags, file.Info.Path));
            offset += cabSize;
        }
        writer.FlushBuffer();
        writer.EnableCompression = false;
        writer.Endian = Endianness.BigEndian;
        var dataInfo = new BlocksAndDirectoryInfo(DataInfo.UncompressedDataHash, writer.BlockInfos, directoryInfo);
        MemoryBinaryIO dataInfoBuffer = new MemoryBinaryIO((int)dataInfo.SerializeSize);
        dataInfo.Serialize(dataInfoBuffer);
        var uncompressedBlocksInfoSize = dataInfoBuffer.Length;
        MemoryBinaryIO compressedDataInfoBuffer = new MemoryBinaryIO((int)uncompressedBlocksInfoSize);
        var compressedBlocksInfoSize = Compression.CompressToBytes(dataInfoBuffer.AsReadOnlySpan, compressedDataInfoBuffer.GetWritableSpan((int)uncompressedBlocksInfoSize), infoPacker);
        compressedDataInfoBuffer.Position = 0;
        writer.WriteBytes(compressedDataInfoBuffer.ReadOnlySlice(0, (int)compressedBlocksInfoSize));
        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision,
            writer.Position, (uint)compressedBlocksInfoSize, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker | ArchiveFlags.BlocksInfoAtTheEnd);
        if (unityCnKey == null && UnityCnKey != null)
        {
            var unityCnMask = VersionJudge1(version)
                ? ArchiveFlags.BlockInfoNeedPaddingAtStart
                : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
            if (unityCnMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
                unityCnMask = (Header.Flags & ArchiveFlags.UnityCNEncryptionNew) != 0 ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            header.Flags &= ~unityCnMask;
        }
        writer.Seek(0);
        header.Serialize(writer);
    }

    /*public int Serialize(string path, CompressionType infoPacker = CompressionType.None,
        CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        DataBuffer db = new DataBuffer(0);
        int size = Serialize(db, infoPacker, dataPacker, unityCnKey);
        db.WriteToFile(path, size);
        return size;
    }*/
    
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