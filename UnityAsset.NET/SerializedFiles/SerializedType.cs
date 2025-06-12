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
    
    public static SerializedType Parse(ref DataBuffer db, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        var typeID = db.ReadInt32();
        var isStrippedType = version >= RefactoredClassId && db.ReadBoolean();
        var scriptTypeIndex = version >= RefactorTypeData ? db.ReadInt16() : (short)-1;
        Hash128? scriptIdHash = null;
        if ((version < RefactorTypeData && typeID < 0) ||
            (version >= RefactorTypeData && typeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && scriptTypeIndex > 0))
        {
            scriptIdHash = new Hash128(ref db);
        }
        var typeHash = new Hash128(ref db);
        List<TypeTreeNode>? nodes = null;
        byte[]? stringBufferBytes = null;
        int[]? typeDependencies = null;
        SerializedTypeReference? typeReference = null;
        if (typeTreeEnabled)
        {
            int typeTreeNodeCount = db.ReadInt32();
            int stringBufferLen = db.ReadInt32();
            nodes = db.ReadList(typeTreeNodeCount, (ref DataBuffer d) => TypeTreeNode.Parse(ref d, version));
            stringBufferBytes = db.ReadBytes(stringBufferLen);
            if (version >= StoresTypeDependencies)
            {
                if (isRefType)
                {
                    typeReference = SerializedTypeReference.Parse(ref db);
                }
                else
                {
                    typeDependencies = db.ReadIntArray(db.ReadInt32());
                }
            }
        }
        return new SerializedType(typeID, isStrippedType, scriptTypeIndex, scriptIdHash, typeHash, isRefType, nodes, stringBufferBytes, typeDependencies, typeReference);
    }

    public void Serialize(ref DataBuffer db, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        db.WriteInt32(TypeID);
        if (version >= RefactoredClassId)
        {
            db.WriteBoolean(IsStrippedType);
        }
        if (version >= RefactorTypeData)
        {
            db.WriteInt16(ScriptTypeIndex);
        }
        if ((version < RefactorTypeData && TypeID < 0) ||
            (version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
        {
            ScriptIdHash?.Serialize(ref db);
        }
        TypeHash.Serialize(ref db);
        if (Nodes == null || StringBufferBytes == null)
        {
            throw new NullReferenceException("Nodes or StringBufferBytes is null");
        }
        if (typeTreeEnabled)
        {
            db.WriteInt32(Nodes.Count);
            db.WriteInt32(StringBufferBytes.Length);
            db.WriteList(Nodes, (ref DataBuffer d, TypeTreeNode node) => node.Serialize(ref d, version));
            db.WriteBytes(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType && TypeDependencies != null)
                {
                    db.WriteIntArrayWithCount(TypeDependencies);
                }
                else if(TypeReference != null)
                {
                    TypeReference.Serialize(ref db);
                }
                else
                {
                    throw new NullReferenceException($"{(isRefType ? "TypeReference" : "TypeDependencies")} is null");
                }
            }
        }
    }

    public long SerializeSize => 39 + 
                                 (Nodes == null || StringBufferBytes == null 
                                     ? 0 
                                     : 8 + Nodes.Sum(n => n.SerializeSize) + StringBufferBytes.Length
                                     + (TypeDependencies == null ? 0 : 4 + TypeDependencies.Length)
                                     + (TypeReference == null ? 0 : 4 + TypeReference.SerializeSize));

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