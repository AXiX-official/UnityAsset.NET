using UnityAsset.NET.IO;

namespace UnityAsset.NET.BundleFiles;

public sealed class BlocksAndCabsInfo
{
    public byte[] UncompressedDataHash;
    public List<StorageBlockInfo> BlocksInfo;
    public List<CabInfo> DirectoryInfo;
    
    public BlocksAndCabsInfo(byte[] uncompressedDataHash,
                              List<StorageBlockInfo> blocksInfo, 
                              List<CabInfo> directoryInfo)
    {
        UncompressedDataHash = uncompressedDataHash;
        BlocksInfo = blocksInfo;
        DirectoryInfo = directoryInfo;
    }

    public static BlocksAndCabsInfo Parse(ref DataBuffer db) => new (
        db.ReadBytes(16),
        db.ReadList(db.ReadInt32(), StorageBlockInfo.Parse),
        db.ReadList(db.ReadInt32(), CabInfo.Parse)
    );
    
    public void Serialize(ref DataBuffer db) {
        db.WriteBytes(UncompressedDataHash);
        db.WriteListWithCount(BlocksInfo, (ref DataBuffer d, StorageBlockInfo info) => info.Serialize(ref d));
        db.WriteListWithCount(DirectoryInfo, (ref DataBuffer d, CabInfo info) => info.Serialize(ref d));
    }

    public long SerializeSize => UncompressedDataHash.Length + 8 + 
                                 BlocksInfo.Sum(item => item.SerializeSize) + 
                                 DirectoryInfo.Sum(item => item.SerializeSize);

    /*public void uncompresseFlags()
    {
        foreach (var block in BlocksInfo)
        {
            //block.flags &= ~StorageBlockFlags.CompressionTypeMask;
            block.Flags &= 0x0;
            block.CompressedSize = block.UncompressedSize;
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
                if (newBlocksInfo.Last().Flags != block.Flags)
                {
                    throw new Exception("Expected all blocks to have the same flags");
                }
                long newSize = (long)newBlocksInfo.Last().UncompressedSize + block.UncompressedSize;
                if (newSize <= uint.MaxValue)
                {
                    newBlocksInfo.Last().UncompressedSize = (uint)newSize;
                    newBlocksInfo.Last().CompressedSize += block.CompressedSize;
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
        
        if (DirectoryInfo.Sum(x => x.Size) != newBlocksInfo.Sum(x => x.UncompressedSize))
        {
            throw new Exception("Expected all blocks size bigger than CAB size");
        }

        BlocksInfo = newBlocksInfo;
        Update();
    }
    
    public void ResizeBlocksInfos()
    {
        List<StorageBlockInfo> newBlocksInfo = new List<StorageBlockInfo>();
        foreach (var block in BlocksInfo)
        {
            if (block.UncompressedSize > 0x00020000)
            {
                uint num = block.UncompressedSize / 0x00020000;
                uint num2 = block.UncompressedSize % 0x00020000;
                for (uint i = 0; i < num; i++)
                {
                    newBlocksInfo.Add(new StorageBlockInfo(0x00020000, 0x00020000, block.Flags));
                }
                if (num2 > 0)
                {
                    newBlocksInfo.Add(new StorageBlockInfo(num2, num2, block.Flags));
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

    public void Update(CompressionType compressionType = CompressionType.None)
    {
        // 写入数据到BlocksInfoBytes
        var BlocksInfoStream = new MemoryStream();
        using AssetWriter writer = new AssetWriter(BlocksInfoStream);
        writer.Write(UncompressedDataHash);
        writer.WriteInt32(BlocksInfo.Count);
        foreach (var block in BlocksInfo)
        {
            //block.Write(writer);
        }
        writer.WriteInt32(DirectoryInfo.Count);
        foreach (var node in DirectoryInfo)
        {
            //node.Write(writer);
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
        
        if (DirectoryInfo[^1].Size + diff <= UInt32.MaxValue && DirectoryInfo[^1].Size + diff > 0)
        {
            if (BlocksInfo[^1].UncompressedSize + diff <= UInt32.MaxValue && BlocksInfo[^1].UncompressedSize + diff > 0)
            {
                if (diff > 0)
                {
                    BlocksInfo[^1].UncompressedSize += (uint)diff;
                    BlocksInfo[^1].CompressedSize += (uint)diff;
                    DirectoryInfo[^1].Size += (uint)diff;
                }
                else
                {
                    BlocksInfo[^1].UncompressedSize -= (uint)-diff;
                    BlocksInfo[^1].CompressedSize -= (uint)-diff;
                    DirectoryInfo[^1].Size -= (uint)-diff;
                }
            }
            else
            {
                if (BlocksInfo[^1].UncompressedSize + diff <= UInt32.MaxValue)
                {
                    BlocksInfo.Add(new StorageBlockInfo((uint)diff, (uint)diff ,BlocksInfo[^1].Flags));
                    DirectoryInfo[^1].Size += (uint)diff;
                }
                else
                {
                    diff += (int)BlocksInfo[^1].UncompressedSize;
                    diff = -diff;
                    BlocksInfo.Remove(BlocksInfo[^1]);
                    BlocksInfo[^1].UncompressedSize -= (uint)diff;
                    BlocksInfo[^1].CompressedSize -= (uint)diff;
                    DirectoryInfo[^1].Size -= (uint)diff;
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
    }*/
}