using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFile;

public sealed class BlocksAndDirectoryInfo
{
    public byte[] UncompressedDataHash;

    public List<StorageBlockInfo> BlocksInfo;
    
    public List<CABInfo> DirectoryInfo;
    
    private uint uncompressedSize;
    
    public List<byte> BlocksInfoBytes;

    public BlocksAndDirectoryInfo(AssetReader reader, Header header, ref bool blocksInfoAtTheEnd)
    {
        if ((header.flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
        {
            blocksInfoAtTheEnd = true;
            long position = reader.Position;
            reader.Position = header.size - header.compressedBlocksInfoSize;
            BlocksInfoBytes = reader.ReadBytes((int)header.compressedBlocksInfoSize).ToList();
            reader.Position = position;
        }
        else //0x40 BlocksAndDirectoryInfoCombined
        {
            Console.WriteLine($"reader.pos {reader.Position}");
            BlocksInfoBytes = reader.ReadBytes((int)header.compressedBlocksInfoSize).ToList();
        }
        
        ReadOnlySpan<byte> blocksInfoCompressedData = BlocksInfoBytes.ToArray();
        var compressionType = (CompressionType)(header.flags & ArchiveFlags.CompressionTypeMask);
        uncompressedSize = header.uncompressedBlocksInfoSize;
        MemoryStream blocksInfoUncompresseddStream = new MemoryStream((int)(uncompressedSize));
        switch (compressionType) //kArchiveCompressionTypeMask
        {
            case CompressionType.None: //None
            {
                blocksInfoUncompresseddStream = new MemoryStream(blocksInfoCompressedData.ToArray());
                break;
            }
            case CompressionType.Lzma: //LZMA
            {
                Compression.DecompressToStream(blocksInfoCompressedData, blocksInfoUncompresseddStream, uncompressedSize, "lzma");
                blocksInfoUncompresseddStream.Position = 0;
                break;
            }
            case CompressionType.Lz4: //LZ4
            case CompressionType.Lz4HC: //LZ4HC
            {
                Compression.DecompressToStream(blocksInfoCompressedData, blocksInfoUncompresseddStream,  uncompressedSize, "lz4");
                blocksInfoUncompresseddStream.Position = 0;
                break;
            }
            default:
                throw new IOException($"Unsupported compression type {compressionType}");
        }
        
        using AssetReader blocksInfoReader = new AssetReader(blocksInfoUncompresseddStream);
        
        UncompressedDataHash = blocksInfoReader.ReadBytes(16);// 除了ENCR
        var blocksInfoCount = blocksInfoReader.ReadInt32();
        BlocksInfo = new List<StorageBlockInfo>();
        for (int i = 0; i < blocksInfoCount; i++)
        {
            BlocksInfo.Add(new StorageBlockInfo(blocksInfoReader));
        }
        
        var directoryInfoCount = blocksInfoReader.ReadInt32();
        DirectoryInfo = new List<CABInfo>();
        for (int i = 0; i < directoryInfoCount; i++)
        {
            DirectoryInfo.Add(new CABInfo(blocksInfoReader));
        }
    }

    public void uncompresseFlags()
    {
        foreach (var block in BlocksInfo)
        {
            //block.flags &= ~StorageBlockFlags.CompressionTypeMask;
            block.flags &= 0x0;
            block.compressedSize = block.uncompressedSize;
        }
        Update();
    }
    
    public void merge()
    {
        List<StorageBlockInfo> newBlocksInfo = new List<StorageBlockInfo>();

        foreach (var block in BlocksInfo)
        {
            if (newBlocksInfo.Count > 0)
            {
                if (newBlocksInfo.Last().flags != block.flags)
                {
                    throw new Exception("Expected all blocks to have the same flags");
                }
                long newSize = (long)newBlocksInfo.Last().uncompressedSize + block.uncompressedSize;
                if (newSize <= uint.MaxValue)
                {
                    newBlocksInfo.Last().uncompressedSize = (uint)newSize;
                    newBlocksInfo.Last().compressedSize += block.compressedSize;
                }
                else
                {
                    newBlocksInfo.Add(block);
                }
            }
            else
            {
                newBlocksInfo.Add(block);
            }
        }
        
        if (DirectoryInfo.Sum(x => x.size) != newBlocksInfo.Sum(x => x.uncompressedSize))
        {
            throw new Exception("Expected all blocks size bigger than CAB size");
        }

        BlocksInfo = newBlocksInfo;
        Update();
    }
    
    public void Split()
    {
        List<StorageBlockInfo> newBlocksInfo = new List<StorageBlockInfo>();
        foreach (var block in BlocksInfo)
        {
            if (block.uncompressedSize > 0x00020000)
            {
                uint num = block.uncompressedSize / 0x00020000;
                uint num2 = block.uncompressedSize % 0x00020000;
                for (uint i = 0; i < num; i++)
                {
                    newBlocksInfo.Add(new StorageBlockInfo(0x00020000, 0x00020000, block.flags));
                }
                if (num2 > 0)
                {
                    newBlocksInfo.Add(new StorageBlockInfo(num2, num2, block.flags));
                }
            }
            else
            {
                newBlocksInfo.Add(block);
            }
        }
        BlocksInfo = newBlocksInfo;
        Update();
    }

    public void Update(string compressionType = "none")
    {
        // 写入数据到BlocksInfoBytes
        var BlocksInfoStream = new MemoryStream();
        using AssetWriter writer = new AssetWriter(BlocksInfoStream);
        writer.Write(UncompressedDataHash);
        writer.WriteInt32(BlocksInfo.Count);
        foreach (var block in BlocksInfo)
        {
            block.Write(writer);
        }
        writer.WriteInt32(DirectoryInfo.Count);
        foreach (var node in DirectoryInfo)
        {
            node.Write(writer);
        }
        
        uncompressedSize = (uint)BlocksInfoStream.Length;
        BlocksInfoBytes = Compression.CompressStream(BlocksInfoStream, compressionType);
    }
    
    public void calculateSize(ref uint uncompressedBlocksInfoSize, ref uint compressedBlocksInfoSize)
    {
        uncompressedBlocksInfoSize = uncompressedSize;
        compressedBlocksInfoSize = (uint)BlocksInfoBytes.Count;
    }

    public void FixSize(int diff)
    {
        
        if (DirectoryInfo[^1].size + diff <= UInt32.MaxValue && DirectoryInfo[^1].size + diff > 0)
        {
            if (BlocksInfo[^1].uncompressedSize + diff <= UInt32.MaxValue && BlocksInfo[^1].uncompressedSize + diff > 0)
            {
                if (diff > 0)
                {
                    BlocksInfo[^1].uncompressedSize += (uint)diff;
                    BlocksInfo[^1].compressedSize += (uint)diff;
                    DirectoryInfo[^1].size += (uint)diff;
                }
                else
                {
                    BlocksInfo[^1].uncompressedSize -= (uint)-diff;
                    BlocksInfo[^1].compressedSize -= (uint)-diff;
                    DirectoryInfo[^1].size -= (uint)-diff;
                }
            }
            else
            {
                if (BlocksInfo[^1].uncompressedSize + diff <= UInt32.MaxValue)
                {
                    BlocksInfo.Add(new StorageBlockInfo((uint)diff, (uint)diff ,BlocksInfo[^1].flags));
                    DirectoryInfo[^1].size += (uint)diff;
                }
                else
                {
                    diff += (int)BlocksInfo[^1].uncompressedSize;
                    diff = -diff;
                    BlocksInfo.Remove(BlocksInfo[^1]);
                    BlocksInfo[^1].uncompressedSize -= (uint)diff;
                    BlocksInfo[^1].compressedSize -= (uint)diff;
                    DirectoryInfo[^1].size -= (uint)diff;
                }
            }
        }
        else
        {
            throw new Exception("DirectoryInfo size overflow");
        }
    }
    
    public void Write(AssetWriter writer)
    {
        writer.Write(BlocksInfoBytes.ToArray());
    }
}