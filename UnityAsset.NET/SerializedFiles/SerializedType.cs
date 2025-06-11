using System.Text;
using static UnityAsset.NET.Enums.SerializedFileFormatVersion;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public sealed class SerializedType
{
    public Int32 TypeID;
    public bool IsStrippedType;
    public Int16 ScriptTypeIndex;
    public Hash128? ScriptIdHash;
    public Hash128 TypeHash;
    public bool IsRefType;
    public List<TypeTreeNode>? Nodes;
    public byte[]? StringBufferBytes;
    public int[]? TypeDependencies;
    public SerializedTypeReference? TypeReference;
    
    public SerializedType(Int32 typeId, bool isStrippedType, Int16 scriptTypeIndex, Hash128? scriptIdHash, Hash128 typeHash, bool isRefType, List<TypeTreeNode>? nodes, byte[]? stringBufferBytes, int[]? typeDependencies, SerializedTypeReference? typeReference)
    {
        TypeID = typeId;
        IsStrippedType = isStrippedType;
        ScriptTypeIndex = scriptTypeIndex;
        ScriptIdHash = scriptIdHash;
        TypeHash = typeHash;
        IsRefType = isRefType;
        Nodes = nodes;
        StringBufferBytes = stringBufferBytes;
        TypeDependencies = typeDependencies;
        TypeReference = typeReference;
    }
    
    public static SerializedType ParseFromReader(AssetReader reader, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        var typeID = reader.ReadInt32();
        var isStrippedType = version >= RefactoredClassId && reader.ReadBoolean();
        var scriptTypeIndex = version >= RefactorTypeData ? reader.ReadInt16() : (short)-1;
        Hash128? scriptIdHash = null;
        if ((version < RefactorTypeData && typeID < 0) ||
            (version >= RefactorTypeData && typeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && scriptTypeIndex > 0))
        {
            scriptIdHash = new Hash128(reader);
        }
        var typeHash = new Hash128(reader);
        List<TypeTreeNode>? nodes = null;
        byte[]? stringBufferBytes = null;
        int[]? typeDependencies = null;
        SerializedTypeReference? typeReference = null;
        if (typeTreeEnabled)
        {
            int typeTreeNodeCount = reader.ReadInt32();
            int stringBufferLen = reader.ReadInt32();
            nodes = reader.ReadList(typeTreeNodeCount, (r) => TypeTreeNode.ParseFromReader(r, version));
            stringBufferBytes = reader.ReadBytes(stringBufferLen);
            if (version >= StoresTypeDependencies)
            {
                if (isRefType)
                {
                    typeReference = SerializedTypeReference.ParseFromReader(reader);
                }
                else
                {
                    typeDependencies = reader.ReadIntArray(reader.ReadInt32());
                }
            }
        }
        return new SerializedType(typeID, isStrippedType, scriptTypeIndex, scriptIdHash, typeHash, isRefType, nodes, stringBufferBytes, typeDependencies, typeReference);
    }

    public void Serialize(AssetWriter writer, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        writer.WriteInt32(TypeID);
        if (version >= RefactoredClassId)
        {
            writer.WriteBoolean(IsStrippedType);
        }
        if (version >= RefactorTypeData)
        {
            writer.WriteInt16(ScriptTypeIndex);
        }
        if ((version < RefactorTypeData && TypeID < 0) ||
            (version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
        {
            ScriptIdHash?.Write(writer);
        }
        TypeHash.Write(writer);
        if (Nodes == null || StringBufferBytes == null)
        {
            throw new NullReferenceException("Nodes or StringBufferBytes is null");
        }
        if (typeTreeEnabled)
        {
            writer.WriteInt32(Nodes.Count);
            writer.WriteInt32(StringBufferBytes.Length);
            writer.WriteList(Nodes, (w, node) => node.Serialize(w, version));
            writer.Write(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType && TypeDependencies != null)
                {
                    writer.WriteIntArrayWithCount(TypeDependencies);
                }
                else if(TypeReference != null)
                {
                    TypeReference.Serialize(writer);
                }
                else
                {
                    throw new NullReferenceException($"{(isRefType ? "TypeReference" : "TypeDependencies")} is null");
                }
            }
        }
    }

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
        sb.AppendFormat("StringBufferBytes: {0} | ", StringBufferBytes.Length);
        sb.AppendFormat("TypeDependencies: {0} | ", TypeDependencies?.Length);
        sb.AppendFormat("TypeReference: {0} | ", TypeReference);
        sb.AppendLine("Nodes:");
        foreach (var node in Nodes)
        {
            sb.AppendLine(node.ToString());
        }
        sb.AppendLine();
        sb.AppendLine("End of Serialized Type");
        return sb.ToString();
    }
}