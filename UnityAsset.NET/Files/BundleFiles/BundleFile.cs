using System.Text.RegularExpressions;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

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

    public static UnityCN? ParseUnityCnInfo(DataBuffer db, Header header, string? key)
    {
        var unityCnMask = VersionJudge1(ParseVersion(header)) 
            ? ArchiveFlags.BlockInfoNeedPaddingAtStart 
            : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
        
        if ((header.Flags & unityCnMask) != 0)
        {
            key ??= Setting.DefaultUnityCNKey;
            if (key == null)
                throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {unityCnMask}");
            return new UnityCN(db, key);
        }
        return null;
    }

    public static void AlignAfterHeader(DataBuffer db, Header header)
    {
        if (header.Version >= 7)
            db.Align(16);
        else // temp fix for 2019.4.x
        {
            var version = ParseVersion(header);
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                db.Align(16);
        }
    }

    public static BlocksAndDirectoryInfo ParseDataInfo(DataBuffer db, Header header)
    {
        Span<byte> blocksInfoBytes = (header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0
            ? db.ReadSpanBytes((int)header.CompressedBlocksInfoSize)
            : db[(int)(header.Size - header.CompressedBlocksInfoSize)..(int)header.Size];
        var compressionType = (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask);
        DataBuffer blocksInfoUncompressedData = new DataBuffer((int)header.UncompressedBlocksInfoSize);
        Compression.DecompressToBytes(blocksInfoBytes, blocksInfoUncompressedData.AsSpan(), compressionType);
        var dataInfo = BlocksAndDirectoryInfo.Parse(blocksInfoUncompressedData);
        if (!VersionJudge1(ParseVersion(header)) && (header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            db.Align(16);
        return dataInfo;
    }

    public static (List<FileWrapper>, uint) ParseFiles(DataBuffer db, BlocksAndDirectoryInfo dataInfo, UnityCN? unityCnInfo = null)
    {
        DataBuffer blocksBuffer = new DataBuffer(dataInfo.BlocksInfo.Sum(block => (int)block.UncompressedSize));
        if (unityCnInfo == null)
            foreach (var blockInfo in dataInfo.BlocksInfo)
                Compression.DecompressToBytes(
                    db.ReadSpanBytes((int)blockInfo.CompressedSize), 
                    blocksBuffer.ReadSpanBytes((int)blockInfo.UncompressedSize), 
                    (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask));
        else
        {
            for (int i = 0; i < dataInfo.BlocksInfo.Count; i++)
            {
                var blockInfo = dataInfo.BlocksInfo[i];
                var compressionType = (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask);
                if (compressionType == CompressionType.Lz4 || compressionType == CompressionType.Lz4HC)
                    unityCnInfo.DecryptAndDecompress(
                        db.ReadSpanBytes((int)blockInfo.CompressedSize), 
                        blocksBuffer.ReadSpanBytes((int)blockInfo.UncompressedSize), 
                        i);
                else
                    throw new IOException($"Unsupported compression type {compressionType} for UnityCN");
            }
        }
        blocksBuffer.Seek(0);

        var crc32 = CRC32.CalculateCRC32(blocksBuffer.AsSpan());
        
        var files = new List<FileWrapper>();
        foreach (var dir in dataInfo.DirectoryInfo)
        {
            if (dir.Path.StartsWith("CAB-") && !dir.Path.EndsWith(".resS"))
            {
                var cabBuffer = blocksBuffer.SliceBuffer((int)dir.Size);
                files.Add(new FileWrapper(SerializedFile.Parse(cabBuffer), dir));
                blocksBuffer.Advance((int)dir.Size);
            }
            else
            {
                files.Add(new FileWrapper(new DataBuffer(blocksBuffer.ReadSpanBytes((int)dir.Size).ToArray()), dir));
            }
        }

        return (files, crc32);
    }

    public BundleFile(Header header, BlocksAndDirectoryInfo dataInfo, List<FileWrapper> files, string? key = null)
    {
        UnityCnKey = key;
        Header = header;
        DataInfo = dataInfo;
        Files = files;
    }

    public BundleFile(DataBuffer db, string? key = null)
    {
        UnityCnKey = key;
        Header = Header.Parse(db);
        UnityCnInfo = ParseUnityCnInfo(db, Header, UnityCnKey);
        AlignAfterHeader(db, Header);
        DataInfo = ParseDataInfo(db, Header);
        (Files, Crc32) = ParseFiles(db, DataInfo, UnityCnInfo);
    }

    public BundleFile(string path, string? key = null) : this(DataBuffer.FromFile(path), key) {}
    
    public void Serialize(DataBuffer db, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || Files == null)
            throw new NullReferenceException("BundleFile has not read completely");
        var directoryInfo = new List<FileEntry>();
        var filesCapacity = Files.Sum(c =>
        {
            if (c.File is DataBuffer hdb) return hdb.Capacity;
            if (c.File is SerializedFile sf) return sf.SerializeSize;
            return 0;
        });
        DataBuffer filesBuffer = new DataBuffer((int)filesCapacity);
        Int64 offset = 0;
        foreach (var file in Files)
        {
            Int64 cabSize;
            switch (file.File)
            {
                case DataBuffer dataBuffer:
                    filesBuffer.WriteBytes(dataBuffer.AsSpan());
                    cabSize = dataBuffer.Length;
                    break;
                case SerializedFile sf:
                    var subBuffer = filesBuffer.SliceBufferToEnd();
                    sf.Serialize(subBuffer);
                    filesBuffer.Advance(subBuffer.Position);
                    cabSize = subBuffer.Position;
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
                for (int i = 0; i < blocksInfo.Count; i++)
                {
                    var blockInfo = blocksInfo[i];
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
        db.EnsureCapacity((int)(header.SerializeSize + 16 + compressedBlocksInfoSize + compressedBlocksDataBuffer.Length + unityCnInfo?.SerializeSize ?? 0));
        header.Serialize(db);
        if (unityCnInfo != null)
            unityCnInfo.Serialize(db);
        if (header.Version >= 7)
            db.Align(16);
        else // temp fix for 2019.4.x
        {
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
                db.Align(16);
        }
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0) //0x40 BlocksAndDirectoryInfoCombined
            db.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        if (!VersionJudge1(version) &&(header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            db.Align(16);
        db.WriteBytes(compressedBlocksDataBuffer.SliceForward(compressedBlocksDataBuffer.Length));
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            db.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        var size = db.Position;
        header.Size = size;
        db.Seek(0);
        header.Serialize(db);
    }

    public void Serialize(string path, CompressionType infoPacker = CompressionType.None,
        CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        DataBuffer db = new DataBuffer(0);
        Serialize(db, infoPacker, dataPacker, unityCnKey);
        db.WriteToFile(path);
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