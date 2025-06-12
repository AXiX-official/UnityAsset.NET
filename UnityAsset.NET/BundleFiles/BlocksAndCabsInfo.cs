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
}