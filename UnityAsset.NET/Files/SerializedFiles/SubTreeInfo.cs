using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

// Based on https://github.com/aelurum/AssetStudioMod
public class SubTreeInfo
{
    public TypeTreeNode[] Nodes;
    public Hash128[] SubTreeDependencies;
    
    public SubTreeInfo(TypeTreeNode[] nodes, Hash128[] subTreeDependencies)
    {
        Nodes = nodes;
        SubTreeDependencies = subTreeDependencies;
    }
    
    public static SubTreeInfo Parse(IReader reader, SerializedFileFormatVersion version)
    {
        SerializedType.ReadTypeTreeBlob(reader, version, out var nodes, out _);
        var subTreeDependencies = reader.ReadArray(r => new Hash128(r));
        return new SubTreeInfo(nodes, subTreeDependencies);
    }
}