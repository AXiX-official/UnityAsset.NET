using System.Text;
using System.Text.RegularExpressions;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using StreamReader = UnityAsset.NET.IO.StreamReader;

namespace UnityAsset.NET.Files;

public class BundleFile
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
    public List<FileWrapper>? Files;
    /// <summary>
    /// Optional UnityCN encryption data
    /// </summary>
    public UnityCN? UnityCnInfo;
    /// <summary>
    /// Optional key for UnityCN encryption
    /// </summary>
    public string? UnityCnKey;

    public uint Crc32;

    public static UnityCN? ParseUnityCnInfo(IReader reader, Header header, string? key)
    {
        var unityCnMask = VersionJudge1(ParseVersion(header)) 
            ? ArchiveFlags.BlockInfoNeedPaddingAtStart 
            : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
        
        if ((header.Flags & unityCnMask) != 0)
        {
            key ??= Setting.DefaultUnityCNKey;
            if (key == null)
                throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {unityCnMask}");
            return new UnityCN(reader, key);
        }
        return null;
    }

    public static void AlignAfterHeader(IReader reader, Header header)
    {
        if (header.Version >= 7)
            reader.Align(16);
        else // temp fix for 2019.4.x
        {
            var version = ParseVersion(header);
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                reader.Align(16);
        }
    }

    public static BlocksAndDirectoryInfo ParseDataInfo(IReader reader, Header header)
    {
        var blocksInfoBytes = (header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0 
            ? reader.ReadOnlySpanBytes((int)header.CompressedBlocksInfoSize) 
            : reader.ReadOnlySlice((int)(header.Size - header.CompressedBlocksInfoSize), (int)header.CompressedBlocksInfoSize);
        var compressionType = (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask);
        MemoryBinaryIO blocksInfoUncompressedData = MemoryBinaryIO.Create((int)header.UncompressedBlocksInfoSize);
        Compression.DecompressToBytes(blocksInfoBytes, blocksInfoUncompressedData.AsWritableSpan, compressionType);
        var dataInfo = BlocksAndDirectoryInfo.Parse(blocksInfoUncompressedData);
        if (!VersionJudge1(ParseVersion(header)) && (header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            reader.Align(16);
        return dataInfo;
    }

    public static (List<FileWrapper>, uint) ParseFiles(IReader reader, BlocksAndDirectoryInfo dataInfo, UnityCN? unityCnInfo = null)
    {
        MemoryBinaryIO blocksBuffer = MemoryBinaryIO.Create(dataInfo.BlocksInfo.Sum(block => (int)block.UncompressedSize));
        if (unityCnInfo == null)
            foreach (var blockInfo in dataInfo.BlocksInfo.AsReadOnlySpan())
                Compression.DecompressToBytes(
                    reader.ReadOnlySpanBytes((int)blockInfo.CompressedSize), 
                    blocksBuffer.GetWritableSpan((int)blockInfo.UncompressedSize), 
                    (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask));
        else
        {
            var blocksInfoSpan = dataInfo.BlocksInfo.AsReadOnlySpan();
            for (int i = 0; i < blocksInfoSpan.Length; i++)
            {
                var blockInfo = blocksInfoSpan[i];
                var compressionType = (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask);
                if (compressionType == CompressionType.Lz4 || compressionType == CompressionType.Lz4HC)
                    unityCnInfo.DecryptAndDecompress(
                        reader.ReadOnlySpanBytes((int)blockInfo.CompressedSize), 
                        blocksBuffer.GetWritableSpan((int)blockInfo.UncompressedSize), 
                        i);
                else
                    throw new IOException($"Unsupported compression type {compressionType} for UnityCN");
            }
        }
        blocksBuffer.Seek(0);

        var crc32 = CRC32.CalculateCRC32(blocksBuffer.AsReadOnlySpan);
        
        var files = new List<FileWrapper>();
        foreach (var dir in dataInfo.DirectoryInfo.AsReadOnlySpan())
            files.Add(new FileWrapper(MemoryBinaryIO.Create(blocksBuffer.ReadOnlySpanBytes((int)dir.Size).ToArray()), dir));

        return (files, crc32);
    }
    
    // crc calculate disabled
    public static (List<FileWrapper>, uint) LazyParseFiles(StreamReader reader, BlocksAndDirectoryInfo dataInfo, UnityCN? unityCnInfo = null)
    {
        var blockStream = new BlockStream(dataInfo.BlocksInfo, reader, unityCnInfo);
        //var crc32 = CRC32.CalculateCRC32(blocksBuffer.AsReadOnlySpan);
        var files = new List<FileWrapper>();
        foreach (var dir in dataInfo.DirectoryInfo.AsReadOnlySpan())
            files.Add(new FileWrapper(new FileEntryStreamReader(blockStream, dir), dir));

        return (files, 0);
    }

    public BundleFile(Header header, BlocksAndDirectoryInfo dataInfo, List<FileWrapper> files, string? key = null)
    {
        UnityCnKey = key;
        Header = header;
        DataInfo = dataInfo;
        Files = files;
    }

    public BundleFile(IReader reader, string? key = null)
    {
        UnityCnKey = key;
        Header = Header.Parse(reader);
        UnityCnInfo = ParseUnityCnInfo(reader, Header, UnityCnKey);
        AlignAfterHeader(reader, Header);
        DataInfo = ParseDataInfo(reader, Header);
        (Files, Crc32) = (reader is StreamReader streamReader) ? LazyParseFiles(streamReader, DataInfo, UnityCnInfo) : ParseFiles(reader, DataInfo, UnityCnInfo);
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
    public BundleFile(string path, string? key = null) : this(new FileStreamReader(path), key) {}
    
    /*public void Serialize(IWriter writer, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || Files == null)
            throw new NullReferenceException("BundleFile has not read completely");
        var directoryInfo = new List<FileEntry>();
        var filesCapacity = Files.Sum(c =>
        {
            if (c.File is IReader r) return r.Length;
            if (c.File is SerializedFile sf) return sf.SerializeSize;
            return 0;
        });
        MemoryBinaryIO filesBuffer = MemoryBinaryIO.Create((int)filesCapacity);
        Int64 offset = 0;
        foreach (var file in Files.AsReadOnlySpan())
        {
            int cabSize;
            switch (file.File)
            {
                case IReader r:
                    cabSize = filesBuffer.WriteBytes(r.);
                    break;
                case SerializedFile sf:
                    cabSize = sf.Serialize(filesBuffer.SliceBufferToEnd());
                    filesBuffer.Advance(cabSize);
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
        DataBuffer compressedBlocksDataBuffer = new DataBuffer((int)blocksSize);
        var defaultChunkSize = dataPacker == CompressionType.Lzma ? UInt32.MaxValue : Setting.DefaultChunkSize;
        while (blocksSize > 0)
        {
            int chunkSize = (int)Math.Min(blocksSize, defaultChunkSize);
            var compressedSize = Compression.CompressToBytes(
                filesBuffer.ReadSpanBytes(chunkSize),
                compressedBlocksDataBuffer.SliceForward(),
                dataPacker);
            blocksInfo.Add(new StorageBlockInfo((UInt32)chunkSize, (UInt32)compressedSize, (StorageBlockFlags)dataPacker));
            blocksSize -= chunkSize;
            compressedBlocksDataBuffer.Advance((int)compressedSize);
        }
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
                    unityCnInfo.EncryptBlock(compressedBlocksDataBuffer.ReadSpanBytes((int)blockInfo.CompressedSize), (int)blockInfo.CompressedSize, i);
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
        DataBuffer dataInfoBuffer = new DataBuffer((int)dataInfo.SerializeSize);
        dataInfo.Serialize(dataInfoBuffer);
        var uncompressedBlocksInfoSize = dataInfoBuffer.Length;
        DataBuffer compressedDataInfoBuffer = new DataBuffer(uncompressedBlocksInfoSize);
        var compressedBlocksInfoSize = Compression.CompressToBytes(dataInfoBuffer.AsSpan(), compressedDataInfoBuffer.AsSpan(), infoPacker);

        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision,
            0, (uint)compressedBlocksInfoSize, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker);
        writer.EnsureCapacity((int)(header.SerializeSize + 16 + compressedBlocksInfoSize + compressedBlocksDataBuffer.Length + unityCnInfo?.SerializeSize ?? 0));
        int size = 0;
        size += header.Serialize(writer);
        if (unityCnInfo != null)
            size += unityCnInfo.Serialize(writer);
        if (header.Version >= 7)
            size += writer.Align(16);
        else // temp fix for 2019.4.x
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                size += writer.Align(16);
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0) //0x40 BlocksAndDirectoryInfoCombined
            size += writer.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        if (!VersionJudge1(version) &&(header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            size += writer.Align(16);
        size += writer.WriteBytes(compressedBlocksDataBuffer.SliceForward(compressedBlocksDataBuffer.Length));
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            size += writer.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        header.Size = size;
        writer.Seek(0);
        header.Serialize(writer);
        return size;
    }*/

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
    
    public static int[] ParseVersion(Header header)
    {
        return Regex.Replace(header.UnityRevision, @"\D", ".")
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
    }

    public static bool VersionJudge1(int[] version)
    {
        return version[0] < 2020 || //2020 and earlier
               (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
               (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
               (version[0] == 2022 && version[1] == 3 && version[2] <= 1); //2022.3.1 and earlier
    }
}