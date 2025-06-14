using System.Text.RegularExpressions;

using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.SerializedFiles;

namespace UnityAsset.NET.BundleFiles;

public class BundleFile
{
    /// <summary>
    /// BundleFile's Header
    /// </summary>
    public Header? Header;

    /// <summary>
    /// Blocks and Cab info
    /// </summary>
    public BlocksAndCabsInfo? DataInfo;

    public List<CabFileWrapper>? CabFiles;

    public UnityCN? UnityCnInfo;

    public string? UnityCnKey;

    public static UnityCN? ParseUnityCnInfo(ref DataBuffer db, Header header, string? key)
    {
        var version = ParseVersion(header);
        var unityCnMask = (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
            ? ArchiveFlags.BlockInfoNeedPaddingAtStart : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
        
        if ((header.Flags & unityCnMask) != 0)
        {
            if (key == null) key = Setting.DefaultUnityCNKey;
            if (key == null)
            {
                throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {unityCnMask}");
            }
            var unityCnInfo = new UnityCN(ref db, key);
            return unityCnInfo;
        }

        return null;
    }

    public static void AlignAfterHeader(ref DataBuffer db, Header header)
    {
        if (header.Version >= 7)
        {
            db.Align(16);
        }
        else
        {
            var version = ParseVersion(header);
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
            {
                // temp fix for 2019.4.x
                db.Align(16);
            }
        }
    }

    public static BlocksAndCabsInfo ParseDataInfo(ref DataBuffer db, Header header)
    {
        Span<byte> blocksInfoBytes;
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
        {
            var position = db.Position;
            db.Seek((int)(header.Size - header.CompressedBlocksInfoSize));
            blocksInfoBytes = db.ReadSpanBytes((int)header.CompressedBlocksInfoSize);
            db.Seek(position);
        }
        else //0x40 BlocksAndDirectoryInfoCombined
        {
            blocksInfoBytes = db.ReadSpanBytes((int)header.CompressedBlocksInfoSize);
        }
        var compressionType = (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask);
        DataBuffer blocksInfoUncompressedData = new DataBuffer((int)header.UncompressedBlocksInfoSize);
        Compression.DecompressToBytes(blocksInfoBytes, blocksInfoUncompressedData.AsSpan(), compressionType);
        var dataInfo = BlocksAndCabsInfo.Parse(ref blocksInfoUncompressedData);
        
        var version = ParseVersion(header);
        if (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
        {
            
        }
        else
        {
            if ((header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                db.Align(16);
            }
        }
        
        return dataInfo;
    }

    public static List<CabFileWrapper> ParseCabFiles(ref DataBuffer db, BlocksAndCabsInfo dataInfo, UnityCN? unityCnInfo = null)
    {
        DataBuffer blocksBuffer = new DataBuffer(dataInfo.BlocksInfo.Sum(block => (int)block.UncompressedSize));
        if (unityCnInfo == null)
        {
            foreach (var blockInfo in dataInfo.BlocksInfo)
            {
                Compression.DecompressToBytes(
                    db.SliceForward((int)blockInfo.CompressedSize), 
                    blocksBuffer.SliceForward((int)blockInfo.UncompressedSize), 
                    (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask));
                db.Advance((int)blockInfo.CompressedSize);
                blocksBuffer.Advance((int)blockInfo.UncompressedSize);
            }
        }
        else
        {
            for (int i = 0; i < dataInfo.BlocksInfo.Count; i++)
            {
                var blockInfo = dataInfo.BlocksInfo[i];
                var compressionType = (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask);
                if (compressionType == CompressionType.Lz4 || compressionType == CompressionType.Lz4HC)
                {
                    unityCnInfo.DecryptAndDecompress(
                        db.SliceForward((int)blockInfo.CompressedSize), 
                        blocksBuffer.SliceForward((int)blockInfo.UncompressedSize), 
                        i);
                    db.Advance((int)blockInfo.CompressedSize);
                    blocksBuffer.Advance((int)blockInfo.UncompressedSize);
                }
                else
                {
                    throw new IOException($"Unsupported compression type {compressionType} for UnityCN");
                }
            }
        }
        blocksBuffer.Seek(0);
        var cabFiles = new List<CabFileWrapper>();
        foreach (var cab in dataInfo.DirectoryInfo)
        {
            if (cab.Path.EndsWith(".resS"))
            {
                cabFiles.Add(new CabFileWrapper(new HeapDataBuffer(blocksBuffer.SliceForward((int)cab.Size).ToArray()), cab));
                blocksBuffer.Advance((int)cab.Size);
            }
            else
            {
                var cabBuffer = blocksBuffer.SliceBuffer((int)cab.Size);
                cabFiles.Add(new CabFileWrapper(SerializedFile.Parse(ref cabBuffer), cab));
                blocksBuffer.Advance((int)cab.Size);
            }
        }
        return cabFiles;
    }

    public BundleFile(Header header, BlocksAndCabsInfo dataInfo, List<CabFileWrapper> cabFiles, string? key = null)
    {
        UnityCnKey = key;
        Header = header;
        DataInfo = dataInfo;
        CabFiles = cabFiles;
    }

    public BundleFile(ref DataBuffer db, string? key = null)
    {
        UnityCnKey = key;
        Header = Header.Parse(ref db);
        UnityCnInfo = ParseUnityCnInfo(ref db, Header, UnityCnKey);
        AlignAfterHeader(ref db, Header);
        DataInfo = ParseDataInfo(ref db, Header);
        CabFiles = ParseCabFiles(ref db, DataInfo, UnityCnInfo);
    }

    public BundleFile(string path, string? key = null)
    {
        var db = DataBuffer.FromFile(path);
        UnityCnKey = key;
        Header = Header.Parse(ref db);
        UnityCnInfo = ParseUnityCnInfo(ref db, Header, UnityCnKey);
        AlignAfterHeader(ref db, Header);
        DataInfo = ParseDataInfo(ref db, Header);
        CabFiles = ParseCabFiles(ref db, DataInfo, UnityCnInfo);
    }
    
    public void Serialize(ref DataBuffer db, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        if (Header == null || DataInfo == null || CabFiles == null)
        {
            throw new NullReferenceException("BundleFile has not read completely");
        }

        var directoryInfo = new List<CabInfo>();
        var cabDataBufferCapacity = CabFiles.Sum(c =>
        {
            if (c.File is HeapDataBuffer hdb) return hdb.Capacity;
            if (c.File is SerializedFile sf) return sf.SerializeSize;
            return 0;
        });
        DataBuffer cabDataBuffer = new DataBuffer((int)cabDataBufferCapacity);
        Int64 offset = 0;
        for (int i = 0; i < CabFiles.Count; i++)
        {
            Int64 cabSize;
            switch (CabFiles[i].File)
            {
                case HeapDataBuffer hdb:
                    cabDataBuffer.WriteBytes(hdb.AsSpan());
                    cabSize = hdb.Length;
                    break;
                case SerializedFile sf:
                    var subBuffer = cabDataBuffer.SliceBufferToEnd();
                    sf.Serialize(ref subBuffer);
                    cabDataBuffer.Advance(subBuffer.Position);
                    cabSize = subBuffer.Position;
                    break;
                default:
                    throw new Exception($"Unexpected type: {CabFiles[i].File.GetType().Name}");
            }
            directoryInfo.Add(new CabInfo(offset, cabSize, DataInfo.DirectoryInfo[i].Flags, DataInfo.DirectoryInfo[i].Path));
            offset += cabSize;
        }
        cabDataBuffer.Seek(0);

        var blocksInfo = new List<StorageBlockInfo>();
        var blocksSize = offset;
        DataBuffer compressedBlocksDataBuffer = new DataBuffer((int)blocksSize);
        var defaultChunkSize = dataPacker == CompressionType.Lzma ? UInt32.MaxValue : Setting.DefaultChunkSize;
        offset = 0;
        while (blocksSize > 0)
        {
            int chunkSize = (int)Math.Min(blocksSize, defaultChunkSize);
            var compressedSize = Compression.CompressToBytes(cabDataBuffer.Slice((int)offset, chunkSize),
                compressedBlocksDataBuffer.SliceForward(), dataPacker);
            blocksInfo.Add(new StorageBlockInfo((UInt32)chunkSize, (UInt32)compressedSize, (StorageBlockFlags)dataPacker));
            blocksSize -= chunkSize;
            offset += chunkSize;
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
                    unityCnInfo.EncryptBlock(compressedBlocksDataBuffer.SliceForward((int)blockInfo.CompressedSize), (int)blockInfo.CompressedSize, i);
                    compressedBlocksDataBuffer.Advance((int)blockInfo.CompressedSize);
                }
                compressedBlocksDataBuffer.Seek(0);
            }
            else
            {
                throw new Exception($"UnityCN Encryption only support Lz4/Lz4HC, but {dataPacker} was set.");
            }
        }
        else if (UnityCnKey != null)
        {
            var unityCnMask = (version[0] < 2020 || //2020 and earlier
                               (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
                               (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
                               (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
                ? ArchiveFlags.BlockInfoNeedPaddingAtStart : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
            if (unityCnMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
            {
                unityCnMask = ((Header.Flags & ArchiveFlags.UnityCNEncryptionNew) != 0) ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            }
            Header.Flags &= ~unityCnMask;
        }
        
        var dataInfo = new BlocksAndCabsInfo(DataInfo.UncompressedDataHash, blocksInfo, directoryInfo);
        DataBuffer dataInfoBuffer = new DataBuffer((int)dataInfo.SerializeSize);
        dataInfo.Serialize(ref dataInfoBuffer);
        var uncompressedBlocksInfoSize = dataInfoBuffer.Length;
        DataBuffer compressedDataInfoBuffer = new DataBuffer(uncompressedBlocksInfoSize);
        var compressedBlocksInfoSize = Compression.CompressToBytes(dataInfoBuffer.AsSpan(), compressedDataInfoBuffer.AsSpan(), infoPacker);

        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision,
            0, (uint)compressedBlocksInfoSize, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker);
        db.EnsureCapacity((int)(header.SerializeSize + 16 + compressedBlocksInfoSize + compressedBlocksDataBuffer.Length + (unityCnInfo == null ? 0: unityCnInfo.SerializeSize)));
        header.Serialize(ref db);
        if (unityCnInfo != null)
        {
            unityCnInfo.Serialize(ref db);
        }
        if (header.Version >= 7)
        {
            db.Align(16);
        }
        else
        {
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
            {
                // temp fix for 2019.4.x
                db.Align(16);
            }
        }
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0) //0x40 BlocksAndDirectoryInfoCombined
        {
            db.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        }
        if (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
        {
            
        }
        else
        {
            if ((header.Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                db.Align(16);
            }
        }
        db.WriteBytes(compressedBlocksDataBuffer.SliceForward(compressedBlocksDataBuffer.Length));
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
        {
            db.WriteBytes(compressedDataInfoBuffer.SliceForward((int)compressedBlocksInfoSize));
        }

        var size = db.Position;
        header.Size = size;
        db.Seek(0);
        header.Serialize(ref db);
    }

    public void Serialize(string path, CompressionType infoPacker = CompressionType.None,
        CompressionType dataPacker = CompressionType.None, string? unityCnKey = null)
    {
        DataBuffer db = new DataBuffer(0);
        Serialize(ref db, infoPacker, dataPacker, unityCnKey);
        db.WriteToFile(path);
    }
    
    public static int[] ParseVersion(Header header)
    {
        return Regex.Replace(header.UnityRevision, @"\D", ".")
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
    }
}