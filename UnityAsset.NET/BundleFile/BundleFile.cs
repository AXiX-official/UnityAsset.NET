using System.Text.RegularExpressions;

using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class BundleFile
{
    /// <summary>
    /// BundleFile header
    /// </summary>
    public Header Header { get; set; }
    
    /// <summary>
    /// Key for UnityCN encryption
    /// </summary>
    public string? UnityCNKey { get; set; }
    
    /// <summary>
    /// Data for UnityCN encryption
    /// </summary>
    public UnityCN? UnityCNInfo { get; set; }
    
    /// <summary>
    /// BlocksAndDirectoryInfo
    /// </summary>
    public BlocksAndDirectoryInfo DataInfo { get; set; }

    public List<MemoryStream> cabStreams { get; set; }
    
    public uint crc32 { get; set; }

    private bool _blocksInfoAtTheEnd;
    
    public bool BlocksInfoAtTheEnd { get => _blocksInfoAtTheEnd; set => _blocksInfoAtTheEnd = value; }
    
    public bool HasBlockInfoNeedPaddingAtStart { get; set; }

    private bool HeaderAligned { get; set; }
    
    private ArchiveFlags mask { get; set; }

    public BundleFile(string path, bool original = false, string? key = null)
        : this(new FileStream(path, FileMode.Open, FileAccess.Read), original, key)
    {
    }

    public BundleFile(Stream input, bool original = false, string? key = null)
    {
        using AssetReader reader = new AssetReader(input);
        
        UnityCNKey = key;
        
        Header = new Header(reader);
        var version = ParseVersion();
        
        if (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
        {
            mask = ArchiveFlags.BlockInfoNeedPaddingAtStart;
            HasBlockInfoNeedPaddingAtStart = false;
        }
        else
        {
            mask = ArchiveFlags.UnityCNEncryption;
            HasBlockInfoNeedPaddingAtStart = true;
        }
        if ((Header.flags & mask) != 0)
        {
            Console.WriteLine($"Encryption flag exist, file is encrypted, attempting to decrypt");
            if (UnityCNKey == null)
            {
                throw new Exception("UnityCN key is required for decryption");
            }
            UnityCNInfo = new UnityCN(reader, UnityCNKey);
            Header.flags &= (ArchiveFlags)~mask;
        }
        
        if (Header.version >= 7)
        {
            reader.AlignStream(16);
            HeaderAligned = true;
        }
        else if (version[0] == 2019 && version[1] == 4) // temp fix for 2019.4.x
        {
            var p = reader.Position;
            var len = 16 - p % 16;
            var bytes = reader.ReadBytes((int)len);
            if (bytes.Any(x => x != 0))
            {
                reader.Position = p;
            }
            else
            {
                HeaderAligned = true;
            }
        }
        
        DataInfo = new BlocksAndDirectoryInfo(reader, Header, ref _blocksInfoAtTheEnd);

        foreach (var blockInfo in DataInfo.BlocksInfo)
        {
            blockInfo.flags &= (StorageBlockFlags)~0x100;
        }
        
        if (HasBlockInfoNeedPaddingAtStart && (Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
        {
            reader.AlignStream(16);
        }
        
        ReadBlocks(reader);
        
        DataInfo.uncompresseFlags();
        DataInfo.merge();
        Header.flags &= (ArchiveFlags)~StorageBlockFlags.CompressionTypeMask;
        DataInfo.calculateSize(ref Header.uncompressedBlocksInfoSize,ref Header.compressedBlocksInfoSize);
        
        crc32 = CalculateCRC32();
    }

    private uint CalculateCRC32()
    {
        uint crc = 0;
        foreach (var cab in cabStreams)
        {
            crc = CRC32.CalculateCRC32(cab, crc);
        }
        return crc;
    }
    
    private string BlocksInfoCompressionType
    {
        get
        {
            var compressionType = (CompressionType)(Header.flags & ArchiveFlags.CompressionTypeMask);
            switch (compressionType)
            {
                case CompressionType.None:
                    return "none";
                case CompressionType.Lzma:
                    return "lzma";
                case CompressionType.Lz4:
                    return "lz4";
                case CompressionType.Lz4HC:
                    return "lz4hc";
                default:
                    throw new IOException($"Unsupported compression type {compressionType}");
            }
        }
        
        set
        {
            Header.flags &= (ArchiveFlags)~StorageBlockFlags.CompressionTypeMask;
            switch (value)
            {
                case "none":
                    Header.flags |= (ArchiveFlags)CompressionType.None;
                    break;
                case "lzma":
                    Header.flags |= (ArchiveFlags)CompressionType.Lzma;
                    break;
                case "lz4":
                    Header.flags |= (ArchiveFlags)CompressionType.Lz4;
                    break;
                case "lz4hc":
                    Header.flags |= (ArchiveFlags)CompressionType.Lz4HC;
                    break;
                default:
                    throw new IOException($"Unsupported compression type {value}");
            }
        }
    }
    
    private string BlocksCompressionType
    {
        set
        {
            var newFlag = CompressionType.None;
            switch (value)
            {
                case "none":
                    break;
                case "lzma":
                    newFlag = CompressionType.Lzma;
                    break;
                case "lz4":
                    newFlag = CompressionType.Lz4;
                    break;
                case "lz4hc":
                    newFlag = CompressionType.Lz4HC;
                    break;
                default:
                    throw new IOException($"Unsupported compression type {value}");
            }

            foreach (var block in DataInfo.BlocksInfo)
            {
                block.flags &= ~StorageBlockFlags.CompressionTypeMask;
                block.flags |= (StorageBlockFlags)newFlag;
            }
        }
    }
    
    private void ReadBlocks(AssetReader reader)
    {
        MemoryStream BlocksStream = new MemoryStream();
        for (int i = 0; i < DataInfo.BlocksInfo.Count; i++)
        {
            var blockInfo = DataInfo.BlocksInfo[i];
            var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
            var encryptedData = reader.ReadBytes((int)blockInfo.compressedSize); 
            if (UnityCNInfo != null)
            {
                UnityCNInfo.DecryptBlock(encryptedData, encryptedData.Length, i);
            }
            ReadOnlySpan<byte> compressedData = encryptedData;
            switch (compressionType)
            {
              case CompressionType.None:
                  {
                      BlocksStream.Write(compressedData);
                      break;
                  }
              case CompressionType.Lzma:
                  {
                      Compression.DecompressToStream(compressedData, BlocksStream, blockInfo.uncompressedSize, "lzma");
                      break;
                  }
              case CompressionType.Lz4:
              case CompressionType.Lz4HC:
                  {
                      Compression.DecompressToStream(compressedData, BlocksStream, blockInfo.uncompressedSize, "lz4");
                      break;
                  }
              default:
                  throw new IOException($"Unsupported compression type {compressionType}");
            }
        }  
        BlocksStream.Position = 0;

        cabStreams = new List<MemoryStream>();
        
        foreach (var cab in DataInfo.DirectoryInfo)
        {
            MemoryStream cabStream = new MemoryStream();
            cabStreams.Add(cabStream);
            BlocksStream.Position = cab.offset;
            BlocksStream.CopyTo(cabStream, cab.size);
            cabStream.Position = 0;
        }
    }
    
    public void WriteToFile(string path, string infoPacker = "none", string dataPacker = "none", bool unityCN = false)
    {
        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        Write(fs, infoPacker, dataPacker, unityCN);
    }
    
    public void Write(Stream output, string infoPacker = "none", string dataPacker = "none", bool unityCN = false)
    {
        MemoryStream compressedStream = new MemoryStream();
        
        fixCRC(crc32, CalculateCRC32());
        
        BlocksCompressionType = dataPacker;

        if (unityCN && !(dataPacker == "lz4" || dataPacker == "lz4hc"))
        {
            throw new Exception("UnityCN encryption requires lz4 or lz4hc compression type");
        }

        if (dataPacker != "none")
        {
            DataInfo.Split();
        }
        
        MemoryStream BlocksStream = new MemoryStream();

        foreach (var cab in cabStreams)
        {
            cab.Position = 0;
            cab.CopyTo(BlocksStream);
        }
        
        BlocksStream.Position = 0;
        
        // compress blocks
        foreach (var block in DataInfo.BlocksInfo)
        {
            MemoryStream uncompressedStream = new MemoryStream();
            BlocksStream.CopyTo(uncompressedStream, block.uncompressedSize);
            var compressedData = Compression.CompressStream(uncompressedStream, dataPacker);
            block.compressedSize = (uint)compressedData.Count;
            compressedStream.Write(compressedData.ToArray(), 0, compressedData.Count);
        }
        
        if (unityCN)
        {
            if (UnityCNKey == null)
            {
                throw new Exception("UnityCN key is required for encryption");
            }
            if (UnityCNInfo == null)
            {
                throw new Exception("TODO: UnityCNInfo is null");
            }
            UnityCNInfo.reset();
            compressedStream.Position = 0;
            using MemoryStream encryptedStream = new MemoryStream();
            for (int i =0; i < DataInfo.BlocksInfo.Count; i++)
            {
                var block = DataInfo.BlocksInfo[i];
                block.flags |= (StorageBlockFlags)0x100;
                var blockData = new byte[block.compressedSize];
                compressedStream.Read(blockData, 0, blockData.Length);
                UnityCNInfo.EncryptBlock(blockData, blockData.Length, i);
                encryptedStream.Write(blockData);
            }
            encryptedStream.Position = 0;
            compressedStream.SetLength(0);
            encryptedStream.CopyTo(compressedStream);
            Header.flags |= mask;
        }
        
        BlocksInfoCompressionType = infoPacker;
        DataInfo.Update(BlocksInfoCompressionType);
        calculateSize(compressedStream.Length, unityCN);
        
        using AssetWriter writer = new AssetWriter(output);
        
        Header.Write(writer);

        if (unityCN && UnityCNInfo != null)
        {
            UnityCNInfo.Write(writer);
        }
        
        if (HeaderAligned)
        {
            writer.AlignStream(16);
        }

        if (!BlocksInfoAtTheEnd)
        {
            DataInfo.Write(writer);
        }
        
        if (HasBlockInfoNeedPaddingAtStart && (Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
        {
            writer.AlignStream(16);
        }
        compressedStream.Position = 0;
        
        writer.WriteStream(compressedStream);
        
        if (BlocksInfoAtTheEnd)
        {
            DataInfo.Write(writer);
        }
    }

    public void fixCRC(uint targetCRC, uint currentCRC)
    {
        if (targetCRC == currentCRC)
        {
            return;
        }
        uint append = CRC32.rCRC(targetCRC, currentCRC);
        var fixCRCBytes = BitConverter.GetBytes(append);
        Array.Reverse(fixCRCBytes);
        
        cabStreams[^1].SetLength(cabStreams[^1].Length + 4);
        cabStreams[^1].Position = cabStreams[^1].Length - 4;
        cabStreams[^1].Write(fixCRCBytes);
        cabStreams[^1].Position = 0;
        DataInfo.FixSize(4);
    }

    private void calculateSize(long BlocksStreamLength, bool unityCN)
    {
        DataInfo.calculateSize(ref Header.uncompressedBlocksInfoSize,ref Header.compressedBlocksInfoSize);
        long size = 0;
        size += Header.CalculateSize();
        if (HeaderAligned)
        {
            var a = size % 16;
            if (a != 0)
            {
                size += 16 - a;
            }
        }

        if (unityCN)
        {
            size += 0x46;
        }
        
        size += Header.compressedBlocksInfoSize;
        
        if (HasBlockInfoNeedPaddingAtStart && (Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
        {
            var a = size % 16;
            if (a != 0)
            {
                size += 16 - a;
            }
        }
        
        size += BlocksStreamLength;
        Header.size = size;
    }
    
    public void Bumbo()
    {
        var blockFlag = DataInfo.BlocksInfo[^1].flags;
        var blockSize = int.MaxValue / 2;
        DataInfo.BlocksInfo.Add(new StorageBlockInfo((uint)blockSize, (uint)blockSize, blockFlag));
        DataInfo.DirectoryInfo[^1].size += blockSize;
        cabStreams[^1].SetLength(cabStreams[^1].Length + blockSize);
        cabStreams[^1].Position = cabStreams[^1].Length - blockSize;
        cabStreams[^1].Write(new byte[blockSize]);
        cabStreams[^1].Position = 0;
    }
    
    private int[] ParseVersion()
    {
        var versionSplit = Regex.Replace(Header.unityRevision, @"\D", ".").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
        return versionSplit.Select(int.Parse).ToArray();
    }
}