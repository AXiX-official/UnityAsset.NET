using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.BundleFiles;

public sealed class BlocksAndDirectoryInfo
{
    public byte[] UncompressedDataHash;
    public StorageBlockInfo[] BlocksInfo;
    public FileEntry[] DirectoryInfo;
    
    public BlocksAndDirectoryInfo(
        byte[] uncompressedDataHash,
        StorageBlockInfo[] blocksInfo, 
        FileEntry[] directoryInfo)
    {
        UncompressedDataHash = uncompressedDataHash;
        BlocksInfo = blocksInfo;
        DirectoryInfo = directoryInfo;
    }

    public static BlocksAndDirectoryInfo Parse(IReader reader) => new (
        reader.ReadBytes(16),
        reader.ReadArray(StorageBlockInfo.Parse),
        reader.ReadArray(FileEntry.Parse)
    );
    
    public void Serialize(IWriter writer)
    {
        writer.WriteBytes(UncompressedDataHash);
        writer.WriteArray(BlocksInfo, (w, info) => info.Serialize(w));
        writer.WriteArray(DirectoryInfo, (w, info) => info.Serialize(w));
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"UncompressedDataHash: {BitConverter.ToString(UncompressedDataHash)}");
        for (int i = 0; i  < BlocksInfo.Length; ++i)
            sb.AppendLine($"Block {i}: {BlocksInfo[i]}");
        var directoryInfoSpan = DirectoryInfo.AsSpan();
        for (int i = 0; i  < directoryInfoSpan.Length; ++i)
            sb.AppendLine($"Directory {i}: {directoryInfoSpan[i]}");
        return sb.ToString();
    }
}