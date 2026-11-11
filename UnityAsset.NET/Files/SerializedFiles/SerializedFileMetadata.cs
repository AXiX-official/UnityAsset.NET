using System.Collections.Concurrent;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedFileMetadata
{
    public string UnityVersion;
    public BuildTarget TargetPlatform;
    public bool TypeTreeEnabled;
    public SerializedType[] Types;
    public AssetFileInfo[] AssetInfos;
    public AssetPPtr[] ScriptTypes;
    public AssetsFileExternal[] Externals;
    public SerializedType[]? RefTypes;
    public string UserInformation;
    public Dictionary<Hash128, List<SerializedType>> ExtractedTypeTrees;
    internal static readonly ConcurrentDictionary<Hash128, SubTreeInfo?> SharedSubTreeInfoDict = new();
    
    public SerializedFileMetadata(string unityVersion, BuildTarget targetPlatform, bool typeTreeEnabled,
        SerializedType[] types, AssetFileInfo[] assetInfos, AssetPPtr[] scriptTypes,
        AssetsFileExternal[] externals,SerializedType[]? refTypes, string userInformation, Dictionary<Hash128, List<SerializedType>> extractedTypeTrees)
    {
        UnityVersion = unityVersion;
        TargetPlatform = targetPlatform;
        TypeTreeEnabled = typeTreeEnabled;
        Types = types;
        AssetInfos = assetInfos;
        ScriptTypes = scriptTypes;
        Externals = externals;
        RefTypes = refTypes;
        UserInformation = userInformation;
        ExtractedTypeTrees = extractedTypeTrees;
    }
    
    public static SerializedFileMetadata Parse(IReader reader, SerializedFileFormatVersion version)
    {
        var unityVersion = reader.ReadNullTerminatedString();
        var targetPlatform = (BuildTarget)reader.ReadUInt32();
        var typeTreeEnabled = reader.ReadBoolean();
        var extractedTypeTrees = new Dictionary<Hash128, List<SerializedType>>();
        var types = reader.ReadArray(
            r => SerializedType.Parse(r, version, typeTreeEnabled, false, extractedTypeTrees)
            );
        int assetCount = reader.ReadInt32();
        reader.Align(4);
        var assetInfos = reader.ReadArray(
            assetCount, 
            r => AssetFileInfo.Parse(r, version, types)
            );
        var scriptTypes = reader.ReadArray(AssetPPtr.Parse);
        var externals = reader.ReadArray(AssetsFileExternal.Parse);
        SerializedType[]? refTypes = version >= SerializedFileFormatVersion.SupportsRefObject ?
            reader.ReadArray(r => SerializedType.Parse(r, version, typeTreeEnabled, true, extractedTypeTrees)) :
            null;
        // Based on https://github.com/aelurum/AssetStudioMod
        if (version >= SerializedFileFormatVersion.SharedSubTreeSupport)
        {
            var sharedSubtreeCount = reader.ReadUInt32();
            for (var i = 0; i < sharedSubtreeCount; i++)
            {
                var subTreeHash = new Hash128(reader);
                var subTreeBlobSize = reader.ReadInt32();
                var subTreeInfo = subTreeBlobSize > 0
                    ? SubTreeInfo.Parse(reader, version)
                    : null; //extracted subTree
                SharedSubTreeInfoDict.TryAdd(subTreeHash, subTreeInfo);
            }
            
            foreach (var serializedType in types)
            {
                if (serializedType.SubTreeHashList.Length == 0)
                    continue;

                serializedType.Nodes = ResolveSubTrees(serializedType.Nodes,
                    serializedType.SubTreeHashList);
            }
        }
        var userInformation = reader.ReadNullTerminatedString();
        return new SerializedFileMetadata(unityVersion, targetPlatform, typeTreeEnabled, types, assetInfos, scriptTypes, externals, refTypes, userInformation, extractedTypeTrees);
    }
    
    // Based on https://github.com/aelurum/AssetStudioMod
    internal static TypeTreeNode[] ResolveSubTrees(TypeTreeNode[] nodes, Hash128[] subTreeHashes)
    {
        var nodeList = nodes.ToList();
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            var isSharedSubTreeNode = ((int)node.TypeFlags & 0x20) != 0;
            if (!isSharedSubTreeNode)
                continue;

            var hashIndex = (int)node.RefTypeHash;
            if (SharedSubTreeInfoDict.TryGetValue(subTreeHashes[hashIndex], out var subTreeInfo) && subTreeInfo is not null)
            {
                if (subTreeInfo.SubTreeDependencies.Length > 0)
                {
                    subTreeInfo.Nodes = ResolveSubTrees(subTreeInfo.Nodes, subTreeInfo.SubTreeDependencies);
                }
                node.TypeFlags = subTreeInfo.Nodes[0].TypeFlags;
                for (var k = 1; k < subTreeInfo.Nodes.Length; k++) //skip Base node
                {
                    var subTreeNode = new TypeTreeNode(subTreeInfo.Nodes[k]); //deep copy
                    subTreeNode.Level += node.Level;
                    subTreeNode.Index = node.Index + (uint)k;
                    nodeList.Insert(i + k, subTreeNode);
                }
                i += subTreeInfo.Nodes.Length - 1;
            }
            else
            {
                throw new IOException($"Can't find a shared subtree for the \"{node.Type}\" type");
            }
        }
        return nodeList.ToArray();
    }

    public void Serialize(IWriter writer, SerializedFileFormatVersion version)
    {
        writer.WriteNullTerminatedString(UnityVersion);
        writer.WriteUInt32((UInt32)TargetPlatform);
        writer.WriteBoolean(TypeTreeEnabled);
        writer.WriteArrayWithCount(Types, (d, type) => type.Serialize(d, version, TypeTreeEnabled, false));
        writer.WriteInt32(AssetInfos.Length);
        writer.Align(4);
        writer.WriteArray(AssetInfos, (d, assetInfo) => assetInfo.Serialize(d, version));
        writer.WriteArrayWithCount(ScriptTypes, (d, assetPPtr) => assetPPtr.Serialize(d));
        writer.WriteArrayWithCount(Externals, (d, external) => external.Serialize(d));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            if (RefTypes == null)
                throw new NullReferenceException("RefTypes is null");
            writer.WriteArrayWithCount(RefTypes, (d, type) => type.Serialize(d, version, TypeTreeEnabled, true));
        }
        writer.WriteNullTerminatedString(UserInformation);
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("UnityVersion: {0} | ", UnityVersion);
        sb.AppendFormat("Platform: {0} | ", TargetPlatform);
        sb.AppendFormat("TypeTreeEnabled: {0} | ", TypeTreeEnabled);
        sb.AppendFormat("Types: {0} | ", Types.Length);
        sb.AppendFormat("AssetInfos: {0} | ", AssetInfos.Length);
        sb.AppendFormat("ScriptTypes: {0} | ", ScriptTypes.Length);
        sb.AppendFormat("Externals: {0} | ", Externals.Length);
        sb.AppendFormat("RefTypes: {0} | ", RefTypes?.Length ?? 0);
        sb.AppendFormat("UserInformation: {0}", UserInformation);
        return sb.ToString();
    }
}