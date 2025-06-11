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

    public List<ICabFile>? CabFiles;

    public UnityCN? UnityCN;

    public string? UnityCNKey;

    public static UnityCN? ParseUnityCN(AssetReader reader, Header header, string? key)
    {
        var version = ParseVersion(header);
        var unityCNMask = (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
            ? ArchiveFlags.BlockInfoNeedPaddingAtStart : ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
        
        if ((header.Flags & unityCNMask) != 0)
        {
            if (key == null)
            {
                throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {unityCNMask}");
            }

            if (unityCNMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
            {
                unityCNMask = ((header.Flags & ArchiveFlags.UnityCNEncryptionNew) != 0) ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            }
            var unityCNInfo = new UnityCN(reader, key);
            header.Flags &= ~unityCNMask;
            return unityCNInfo;
        }

        return null;
    }

    public static void AlignAfterHeader(AssetReader reader, Header header)
    {
        if (header.Version >= 7)
        {
            reader.Align(16);
        }
        else
        {
            var version = ParseVersion(header);
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
            {
                // temp fix for 2019.4.x
                reader.Align(16);
            }
        }
    }

    public static BlocksAndCabsInfo ParseDataInfo(AssetReader reader, Header header)
    {
        byte[] blocksInfoBytes;
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
        {
            long position = reader.Position;
            reader.Position = header.Size - header.CompressedBlocksInfoSize;
            blocksInfoBytes = reader.ReadBytes((int)header.CompressedBlocksInfoSize);
            reader.Position = position;
        }
        else //0x40 BlocksAndDirectoryInfoCombined
        {
            blocksInfoBytes = reader.ReadBytes((int)header.CompressedBlocksInfoSize);
        }
        var compressionType = (CompressionType)(header.Flags & ArchiveFlags.CompressionTypeMask);
        using MemoryStream blocksInfoUncompressedStream = new MemoryStream((int)header.UncompressedBlocksInfoSize);
        Compression.DecompressToStream(blocksInfoBytes, blocksInfoUncompressedStream, header.UncompressedBlocksInfoSize, compressionType);
        using AssetReader blocksInfoReader = new AssetReader(blocksInfoUncompressedStream);
        
        var dataInfo = BlocksAndCabsInfo.ParseFromReader(blocksInfoReader);
        
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
                reader.Align(16);
            }
        }
        
        return dataInfo;
    }

    public static List<ICabFile> ParseCabFiles(AssetReader reader, BlocksAndCabsInfo dataInfo, UnityCN? unityCN = null)
    {
        byte[] blocksBuffer = new byte[dataInfo.BlocksInfo.Sum(block => (int)block.UncompressedSize)];
        int bufferOffset = 0;
        if (unityCN == null)
        {
            foreach (var blockInfo in dataInfo.BlocksInfo)
            {
                Compression.DecompressToBytes(
                    reader.ReadBytes((int)blockInfo.CompressedSize), 
                    blocksBuffer.AsSpan(bufferOffset, (int)blockInfo.UncompressedSize), 
                    (CompressionType)(blockInfo.Flags & StorageBlockFlags.CompressionTypeMask));
                bufferOffset += (int)blockInfo.UncompressedSize;
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
                    unityCN.DecryptAndDecompress(reader.ReadBytes((int)blockInfo.CompressedSize), blocksBuffer.AsSpan(bufferOffset, (int)blockInfo.UncompressedSize), i);
                    bufferOffset += (int)blockInfo.UncompressedSize;
                }
                else
                {
                    throw new IOException($"Unsupported compression type {compressionType} for UnityCN");
                }
            }
        }
        
        var cabFiles = new List<ICabFile>();
        foreach (var cab in dataInfo.DirectoryInfo)
        {
            byte[] buffer = new byte[cab.Size];
            Array.Copy(blocksBuffer, cab.Offset, buffer, 0, cab.Size);
            AssetReader cabReader = new AssetReader(buffer);
            if (cab.Path.EndsWith(".resS"))
            {
                cabFiles.Add(cabReader);
            }
            else
            {
                cabFiles.Add(SerializedFile.ParseFromReader(cabReader));
            }
        }
        return cabFiles;
    }

    public BundleFile(Header header, BlocksAndCabsInfo dataInfo, List<ICabFile> cabFiles, string? key = null)
    {
        UnityCNKey = key;
        Header = header;
        DataInfo = dataInfo;
        CabFiles = cabFiles;
    }

    public BundleFile(AssetReader reader, string? key = null)
    {
        UnityCNKey = key;
        Header = Header.ParseFromReader(reader);
        UnityCN = ParseUnityCN(reader, Header, UnityCNKey);
        AlignAfterHeader(reader, Header);
        DataInfo = ParseDataInfo(reader, Header);
        CabFiles = ParseCabFiles(reader, DataInfo, UnityCN);
    }
    
    public BundleFile(string path, string? key = null) : this(new AssetReader(path), key) {}
    
    public void Serialize(string path, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None)
    {
        Serialize(new AssetWriter(path), infoPacker, dataPacker);
    }
    
    public void Serialize(AssetWriter writer, CompressionType infoPacker = CompressionType.None, CompressionType dataPacker = CompressionType.None, string? unityCNKey = null)
    {
        if (Header == null || DataInfo == null || CabFiles == null)
        {
            throw new NullReferenceException("BundleFile has not read completely");
        }

        var directoryInfo = new List<CabInfo>();
        using MemoryStream cabStream = new MemoryStream();
        Int64 offset = 0;
        for (int i = 0; i < CabFiles.Count; i++)
        {
            Int64 cabSize = 0;
            if (CabFiles[i] is AssetReader reader)
            {
                reader.BaseStream.Position = 0;
                reader.BaseStream.CopyTo(cabStream);
                cabSize = (int)reader.BaseStream.Length;
            }
            else if (CabFiles[i] is SerializedFile sf)
            {
                AssetWriter w = new AssetWriter(cabStream);
                w.Position = cabStream.Length;
                var p = w.Position;
                sf.Serialize(w);
                cabSize = w.Position - p;
            }
            else
            {
                throw new Exception($"Unexpect type cab");
            }
            directoryInfo.Add(new CabInfo(offset, cabSize, DataInfo.DirectoryInfo[i].Flags, DataInfo.DirectoryInfo[i].Path));
            offset += cabSize;
        }
        cabStream.Position = 0;

        var blocksInfo = new List<StorageBlockInfo>();
        var blocksSize = cabStream.Length;
        byte[] uncompressedData = cabStream.ToArray();
        using MemoryStream compressedStream = new MemoryStream();
        var defaultChunkSize = dataPacker == CompressionType.Lzma ? UInt32.MaxValue : Setting.DefaultChunkSize;
        offset = 0;
        while (blocksSize > 0)
        {
            int chunkSize = (int)Math.Min(blocksSize, defaultChunkSize);
            var compressedSize = Compression.CompressStream(uncompressedData.AsSpan((int)offset, chunkSize), compressedStream, dataPacker);
            blocksInfo.Add(new StorageBlockInfo((UInt32)chunkSize, (UInt32)compressedSize, (StorageBlockFlags)dataPacker));
            blocksSize -= chunkSize;
            offset += chunkSize;
        }
        compressedStream.Position = 0;

        UnityCN? unityCn = null;
        if (unityCNKey != null)
        {
            unityCn = new UnityCN(unityCNKey);
            if (dataPacker == CompressionType.Lz4 || dataPacker == CompressionType.Lz4HC)
            {
            }
            else
            {
                throw new Exception($"UnityCN Encryption only support Lz4/Lz4HC, but {dataPacker} was set.");
            }
        }
        
        var dataInfo = new BlocksAndCabsInfo(DataInfo.UncompressedDataHash, blocksInfo, directoryInfo);
        using MemoryStream dataInfoStream = new MemoryStream();
        AssetWriter dataInfoWriter = new AssetWriter(dataInfoStream);
        dataInfo.Serialize(dataInfoWriter);
        var uncompressedBlocksInfoSize = dataInfoStream.Length;
        using MemoryStream compressedDataInfoStream = new MemoryStream();
        var compressedBlocksInfoSize = Compression.CompressStream(dataInfoStream.ToArray(), compressedDataInfoStream, infoPacker);
        compressedDataInfoStream.Position = 0;

        var header = new Header(Header.Signature, Header.Version, Header.UnityVersion, Header.UnityRevision,
            0, (uint)compressedBlocksInfoSize, (uint)uncompressedBlocksInfoSize,
            (Header.Flags & ~ArchiveFlags.CompressionTypeMask) | (ArchiveFlags)infoPacker);
        header.Serialize(writer);
        var version = ParseVersion(header);
        if (header.Version >= 7)
        {
            writer.Align(16);
        }
        else
        {
            if (version[0] == 2019 && version[1] == 4 && version[2] >= 30)
            {
                // temp fix for 2019.4.x
                writer.Align(16);
            }
        }
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) == 0) //0x40 BlocksAndDirectoryInfoCombined
        {
            writer.WriteStream(compressedDataInfoStream);
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
                writer.Align(16);
            }
        }
        writer.WriteStream(compressedStream);
        if ((header.Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
        {
            writer.WriteStream(compressedDataInfoStream);
        }

        var size = writer.Position;
        header.Size = size;
        writer.Position = 0;
        header.Serialize(writer);
        writer.Flush();
    }
    
    public static int[] ParseVersion(Header header)
    {
        return Regex.Replace(header.UnityRevision, @"\D", ".")
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
    }
}