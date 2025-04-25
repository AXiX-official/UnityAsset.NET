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
    
    private ArchiveFlags UnityCNMask { get; set; }

    public BundleFile(string path, string? key = null)
        : this(new FileStream(path, FileMode.Open, FileAccess.Read), key)
    {
    }

    public BundleFile(byte[] data, string? key = null)
    {
        using AssetReader reader = new AssetReader(data);
        
        UnityCNKey = key;
        
        Header = new Header(reader);
        ReadBundleWithHeader(reader, Header, key);
    }

    public BundleFile(Stream input, string? key = null)
    {
        using AssetReader reader = new AssetReader(input);
        
        UnityCNKey = key;
        
        Header = new Header(reader);
        ReadBundleWithHeader(reader, Header, key);
    }
    
    public BundleFile(AssetReader reader,Header header, string? key = null)
    {
        UnityCNKey = key;
        Header = header;
        ReadBundleWithHeader(reader, header, key);
    }

    private void ReadBundleWithHeader(AssetReader reader, Header header, string? key = null)
    {
        var version = ParseVersion();
        
        if (version[0] < 2020 || //2020 and earlier
            (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
            (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
            (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
        {
            UnityCNMask = ArchiveFlags.BlockInfoNeedPaddingAtStart;
            HasBlockInfoNeedPaddingAtStart = false;
        }
        else
        {
            UnityCNMask = ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew;
            HasBlockInfoNeedPaddingAtStart = true;
        }
        if ((Header.flags & UnityCNMask) != 0)
        {
            //Console.WriteLine($"Encryption flag exist, file is encrypted, attempting to decrypt");
            if (UnityCNKey == null)
            {
                throw new Exception($"UnityCN key is required for decryption. UnityCN Flag Mask: {UnityCNMask}");
            }

            if (UnityCNMask == (ArchiveFlags.UnityCNEncryption | ArchiveFlags.UnityCNEncryptionNew))
            {
                UnityCNMask = ((Header.flags & ArchiveFlags.UnityCNEncryptionNew) != 0) ?
                    ArchiveFlags.UnityCNEncryptionNew : ArchiveFlags.UnityCNEncryption;
            }
            UnityCNInfo = new UnityCN(reader, UnityCNKey);
            Header.flags &= (ArchiveFlags)~UnityCNMask;
        }
        
        if (Header.version >= 7)
        {
            reader.Align(16);
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
            reader.Align(16);
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
        int totalSize = DataInfo.BlocksInfo.Sum(block => (int)block.uncompressedSize);
        byte[] blocksBuffer = new byte[totalSize];
        int bufferOffset = 0;
        for (int i = 0; i < DataInfo.BlocksInfo.Count; i++)
        {
            var blockInfo = DataInfo.BlocksInfo[i];
            var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
            var encryptedData = reader.ReadBytes((int)blockInfo.compressedSize);
            ReadOnlySpan<byte> compressedData = encryptedData;
            Span<byte> decompressedData = blocksBuffer.AsSpan(bufferOffset, (int)blockInfo.uncompressedSize);
            switch (compressionType)
            {
              case CompressionType.None:
                  {
                      compressedData.CopyTo(decompressedData);
                      break;
                  }
              case CompressionType.Lzma:
                  {
                      
                      Compression.DecompressToBytes(compressedData, decompressedData, "lzma");
                      break;
                  }
              case CompressionType.Lz4:
              case CompressionType.Lz4HC:
                  {
                      if (UnityCNInfo != null)
                      {
                          UnityCNInfo.DecryptAndDecompress(compressedData, decompressedData, i);
                      }
                      else
                      {
                          Compression.DecompressToBytes(compressedData, decompressedData, "lz4");
                      }
                      break;
                  }
              default:
                  throw new IOException($"Unsupported compression type {compressionType}");
            }
            bufferOffset += (int)blockInfo.uncompressedSize;
        }

        cabStreams = new List<MemoryStream>();
        
        foreach (var cab in DataInfo.DirectoryInfo)
        {
            MemoryStream cabStream = new MemoryStream();
            cabStreams.Add(cabStream);
            //BlocksStream.Position = cab.offset;
            //BlocksStream.CopyTo(cabStream, cab.size);
            cabStream.Write(blocksBuffer, (int)cab.offset, (int)cab.size);
            cabStream.Position = 0;
        }
    }
    
    public void WriteToFile(string path, string infoPacker = "none", string dataPacker = "none", bool unityCN = false, string key = "")
    {
        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        Write(fs, infoPacker, dataPacker, unityCN, key);
    }
    
    public void Write(Stream output, string infoPacker = "none", string dataPacker = "none", bool unityCN = false, string key = "")
    {
        MemoryStream compressedStream = new MemoryStream();
        
        //fixCRC(crc32, CalculateCRC32());
        
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
            if (UnityCNInfo == null)
            {
                if (key != "")
                {
                    UnityCNKey = key;
                    UnityCNInfo = new UnityCN(UnityCNKey);
                }
                else
                {
                    throw new Exception("UnityCN key is required for encryption");
                }
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
            Header.flags |= UnityCNMask;
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
        var fixCRCBytes = CRC32.rCRC(targetCRC, currentCRC);
        
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
    
    private int[] ParseVersion()
    {
        var versionSplit = Regex.Replace(Header.unityRevision, @"\D", ".").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
        return versionSplit.Select(int.Parse).ToArray();
    }
}