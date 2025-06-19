using System.Runtime.InteropServices;
using System.Text;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class BlocksAndDirectoryInfo
{
    public byte[] UncompressedDataHash;
    public List<StorageBlockInfo> BlocksInfo;
    public List<FileEntry> DirectoryInfo;
    
    public BlocksAndDirectoryInfo(byte[] uncompressedDataHash,
                              List<StorageBlockInfo> blocksInfo, 
                              List<FileEntry> directoryInfo)
    {
        UncompressedDataHash = uncompressedDataHash;
        BlocksInfo = blocksInfo;
        DirectoryInfo = directoryInfo;
    }

    public static BlocksAndDirectoryInfo Parse(IReader reader) => new (
        reader.ReadBytes(16),
        reader.ReadList(reader.ReadInt32(), StorageBlockInfo.Parse),
        reader.ReadList(reader.ReadInt32(), FileEntry.Parse)
    );
    
    public void Serialize(IWriter writer)
    {
        writer.WriteBytes(UncompressedDataHash);
        writer.WriteListWithCount(BlocksInfo, (w, info) => info.Serialize(w));
        writer.WriteListWithCount(DirectoryInfo, (w, info) => info.Serialize(w));
    }

    public long SerializeSize => UncompressedDataHash.Length + 8 + 
                                 BlocksInfo.Sum(item => item.SerializeSize) + 
                                 DirectoryInfo.Sum(item => item.SerializeSize);
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"UncompressedDataHash: {BitConverter.ToString(UncompressedDataHash)}");
        var blockInfoSpan = BlocksInfo.AsSpan();
        for (int i = 0; i  < blockInfoSpan.Length; ++i)
            sb.AppendLine($"Block {i}: {blockInfoSpan[i]}");
        var directoryInfoSpan = DirectoryInfo.AsSpan();
        for (int i = 0; i  < directoryInfoSpan.Length; ++i)
            sb.AppendLine($"Directory {i}: {directoryInfoSpan[i]}");
        return sb.ToString();
    }
}