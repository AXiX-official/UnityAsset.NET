using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree;
using static UnityAsset.NET.Enums.SerializedFileFormatVersion;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedType
{
    public Int32 TypeID;
    public bool IsStrippedType;
    public Int16 ScriptTypeIndex;
    public Hash128? ScriptIdHash;
    public Hash128 TypeHash;
    public bool IsRefType;
    public List<TypeTreeNode> Nodes;
    public byte[]? StringBufferBytes;
    public int[]? TypeDependencies;
    public SerializedTypeReference? TypeReference;
    
    public SerializedType(Int32 typeId, bool isStrippedType, Int16 scriptTypeIndex, Hash128? scriptIdHash, Hash128 typeHash, bool isRefType, List<TypeTreeNode> nodes, byte[]? stringBufferBytes, int[]? typeDependencies, SerializedTypeReference? typeReference)
    {
        TypeID = typeId;
        IsStrippedType = isStrippedType;
        ScriptTypeIndex = scriptTypeIndex;
        ScriptIdHash = scriptIdHash;
        TypeHash = typeHash;
        IsRefType = isRefType;
        Nodes = TypeTreeCache.GetOrAddNodes(typeHash, nodes);
        StringBufferBytes = stringBufferBytes;
        TypeDependencies = typeDependencies;
        TypeReference = typeReference;
    }
    
    public static SerializedType Parse(IReader reader, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        var typeID = reader.ReadInt32();
        var isStrippedType = reader.ReadBoolean();
        var scriptTypeIndex = reader.ReadInt16();
        Hash128? scriptIdHash = null;
        if ((version >= RefactorTypeData && typeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && scriptTypeIndex >= 0))
        {
            scriptIdHash = new Hash128(reader); 
        }
        var typeHash = new Hash128(reader);
        List<TypeTreeNode> nodes = new List<TypeTreeNode>();
        byte[]? stringBufferBytes = null;
        int[]? typeDependencies = null;
        SerializedTypeReference? typeReference = null;
        if (typeTreeEnabled)
        {
            int typeTreeNodeCount = reader.ReadInt32();
            int stringBufferLen = reader.ReadInt32();
            nodes = reader.ReadList(typeTreeNodeCount, TypeTreeNode.Parse);
            stringBufferBytes = reader.ReadBytes(stringBufferLen);
            MemoryReader sr = new MemoryReader(stringBufferBytes);
            for (int i = 0; i < typeTreeNodeCount; i++)
            {
                var node = nodes[i];
                node.Name = ReadString(sr, node.NameStringOffset);
                node.Type = ReadString(sr, node.TypeStringOffset);
            }

            if (nodes[0].Level != 0)
            {
                throw new Exception(
                    $"The first node of TypeTreeNodes should have a level of 0 but gets {nodes[0].Level}");
            }
            
            if (version >= StoresTypeDependencies)
            {
                if (isRefType)
                    typeReference = SerializedTypeReference.Parse(reader);
                else
                    typeDependencies = reader.ReadIntArray(reader.ReadInt32());
            }
        }
        else
        {
            throw new Exception($"Unexpected typeTreeEnabled false.");
        }
        return new SerializedType(typeID, isStrippedType, scriptTypeIndex, scriptIdHash, typeHash, isRefType, nodes, stringBufferBytes, typeDependencies, typeReference);
    }

    private static string ReadString(IReader reader, uint value)
    {
        if ((value & 0x80000000) == 0)
        {
            reader.Seek((int)value);
            return reader.ReadNullTerminatedString();
        }
        var offset = value & 0x7FFFFFFF;
        if (CommonString.StringBuffer.TryGetValue(offset, out var str))
            return str;
        return offset.ToString();
    }

    /*public void Serialize(IWriter writer, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        writer.WriteInt32(TypeID);
        writer.WriteBoolean(IsStrippedType);
        writer.WriteInt16(ScriptTypeIndex);
        if ((version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
            ScriptIdHash?.Serialize(writer);
        TypeHash.Serialize(writer);
        if (Nodes == null || StringBufferBytes == null)
            throw new NullReferenceException("Nodes or StringBufferBytes is null");
        if (typeTreeEnabled)
        {
            writer.WriteInt32(Nodes.Count);
            writer.WriteInt32(StringBufferBytes.Length);
            writer.WriteList(Nodes, (w, node) => node.Serialize(w));
            writer.WriteBytes(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType && TypeDependencies != null)
                    writer.WriteIntArrayWithCount(TypeDependencies);
                else if(TypeReference != null)
                    TypeReference.Serialize(writer);
                else
                    throw new NullReferenceException($"{(isRefType ? "TypeReference" : "TypeDependencies")} is null");
            }
        }
    }

    public long SerializeSize => 39 + 
                                 (Nodes == null || StringBufferBytes == null 
                                     ? 0 
                                     : 8 + Nodes.Sum(n => n.SerializeSize) + StringBufferBytes.Length
                                     + (TypeDependencies == null ? 0 : 4 + TypeDependencies.Length)
                                     + (TypeReference == null ? 0 : 4 + TypeReference.SerializeSize));
    */
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized Type:");
        sb.AppendFormat("TypeID: {0} | ", TypeID);
        sb.AppendFormat("IsStrippedType: {0} | ", IsStrippedType);
        sb.AppendFormat("ScriptTypeIndex: {0} | ", ScriptTypeIndex);
        sb.AppendFormat("ScriptIdHash: {0} | ", ScriptIdHash?.ToString() ?? "null");
        sb.AppendFormat("TypeHash: {0} | ", TypeHash);
        sb.AppendFormat("IsRefType: {0} | ", IsRefType);
        sb.AppendFormat("Nodes: {0} | ", Nodes.Count);
        sb.AppendFormat("StringBufferBytes: {0} | ", StringBufferBytes?.Length ?? 0);
        sb.AppendFormat("TypeDependencies: {0} | ", TypeDependencies?.Length ?? 0);
        sb.AppendFormat("TypeReference: {0} | ", TypeReference);
        sb.AppendLine("Nodes:");
        foreach (var node in Nodes.AsSpan())
            sb.AppendLine(node.ToString());
        sb.AppendLine();
        return sb.ToString();
    }
}