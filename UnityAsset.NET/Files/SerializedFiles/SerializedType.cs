using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
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
    public List<TypeTreeNode>? Nodes;
    public TypeTreeNode? TypeTree;
    public byte[]? StringBufferBytes;
    public int[]? TypeDependencies;
    public SerializedTypeReference? TypeReference;
    
    public SerializedType(Int32 typeId, bool isStrippedType, Int16 scriptTypeIndex, Hash128? scriptIdHash, Hash128 typeHash, bool isRefType, List<TypeTreeNode>? nodes, byte[]? stringBufferBytes, TypeTreeNode? typeTree, int[]? typeDependencies, SerializedTypeReference? typeReference)
    {
        TypeID = typeId;
        IsStrippedType = isStrippedType;
        ScriptTypeIndex = scriptTypeIndex;
        ScriptIdHash = scriptIdHash;
        TypeHash = typeHash;
        IsRefType = isRefType;
        Nodes = nodes;
        TypeTree = typeTree;
        StringBufferBytes = stringBufferBytes;
        TypeDependencies = typeDependencies;
        TypeReference = typeReference;
    }
    
    public static SerializedType Parse(DataBuffer db, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        var typeID = db.ReadInt32();
        var isStrippedType = db.ReadBoolean();
        var scriptTypeIndex = db.ReadInt16();
        Hash128? scriptIdHash = null;
        if ((version >= RefactorTypeData && typeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && scriptTypeIndex > 0))
            scriptIdHash = new Hash128(db);
        var typeHash = new Hash128(db);
        List<TypeTreeNode>? nodes = null;
        byte[]? stringBufferBytes = null;
        TypeTreeNode? typeTree = null;
        int[]? typeDependencies = null;
        SerializedTypeReference? typeReference = null;
        if (typeTreeEnabled)
        {
            int typeTreeNodeCount = db.ReadInt32();
            int stringBufferLen = db.ReadInt32();
            nodes = db.ReadList(typeTreeNodeCount, TypeTreeNode.Parse);
            stringBufferBytes = db.ReadBytes(stringBufferLen);
            DataBuffer sdb = new DataBuffer(stringBufferBytes, canGrow: false);
            for (int i = 0; i < typeTreeNodeCount; i++)
            {
                var node = nodes[i];
                node.Name = ReadString(sdb, node.NameStringOffset);
                node.Type = ReadString(sdb, node.TypeStringOffset);
            }
            if (nodes[0].Level != 0)
                throw new Exception(
                    $"The first node of TypeTreeNodes should have a level of 0 but gets {nodes[0].Level}");
            var parent = nodes[0];
            for (int i = 1; i < typeTreeNodeCount; i++)
            {
                while (nodes[i].Level <= parent.Level)
                    parent = parent.Parent;
                nodes[i].Parent = parent;
                parent.Children ??= new ();
                parent.Children.Add(nodes[i]);
                parent = nodes[i];
            }
            typeTree = nodes[0];
            if (version >= StoresTypeDependencies)
            {
                if (isRefType)
                    typeReference = SerializedTypeReference.Parse(db);
                else
                    typeDependencies = db.ReadIntArray(db.ReadInt32());
            }
        }
        return new SerializedType(typeID, isStrippedType, scriptTypeIndex, scriptIdHash, typeHash, isRefType, nodes, stringBufferBytes, typeTree, typeDependencies, typeReference);
    }

    private static string ReadString(DataBuffer db, uint value)
    {
        if ((value & 0x80000000) == 0)
        {
            db.Seek((int)value);
            return db.ReadNullTerminatedString();
        }
        var offset = value & 0x7FFFFFFF;
        if (CommonString.StringBuffer.TryGetValue(offset, out var str))
            return str;
        return offset.ToString();
    }

    public void Serialize(DataBuffer db, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        db.WriteInt32(TypeID);
        db.WriteBoolean(IsStrippedType);
        db.WriteInt16(ScriptTypeIndex);
        if ((version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
            ScriptIdHash?.Serialize(db);
        TypeHash.Serialize(db);
        if (Nodes == null || StringBufferBytes == null)
            throw new NullReferenceException("Nodes or StringBufferBytes is null");
        if (typeTreeEnabled)
        {
            db.WriteInt32(Nodes.Count);
            db.WriteInt32(StringBufferBytes.Length);
            db.WriteList(Nodes, (DataBuffer d, TypeTreeNode node) => node.Serialize(d));
            db.WriteBytes(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType && TypeDependencies != null)
                    db.WriteIntArrayWithCount(TypeDependencies);
                else if(TypeReference != null)
                    TypeReference.Serialize(db);
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
            sb.AppendLine(node.ToString());
        sb.AppendLine();
        sb.AppendLine("End of Serialized Type");
        return sb.ToString();
    }
}