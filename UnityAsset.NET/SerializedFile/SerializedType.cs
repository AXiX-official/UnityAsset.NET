using System.Text;
using static UnityAsset.NET.Enums.SerializedFileFormatVersion;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedType
{
    public int TypeID;
    
    public bool IsStrippedType;
    
    public short ScriptTypeIndex;

    public Hash128? ScriptIdHash;
    
    public Hash128 TypeHash;
    
    public bool IsRefType;
    
    public List<TypeTreeNode> Nodes;
    
    public byte[] StringBufferBytes;

    public int[] TypeDependencies;

    public SerializedTypeReference TypeReference;
    
    public SerializedType(AssetReader reader, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        TypeID = reader.ReadInt32();
        
        if (version >= RefactoredClassId)
        {
            IsStrippedType = reader.ReadBoolean();
        }
        
        if (version >= RefactorTypeData)
        {
            ScriptTypeIndex = reader.ReadInt16();
        }
        else
        {
            ScriptTypeIndex = -1;
        }
        
        if ((version < RefactorTypeData && TypeID < 0) ||
            (version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
        {
            ScriptIdHash = new Hash128(reader);
        }
        
        TypeHash = new Hash128(reader);
        IsRefType = isRefType;

        if (typeTreeEnabled)
        {
            int typeTreeNodeCount = reader.ReadInt32();
            int stringBufferLen = reader.ReadInt32();
            Nodes = new List<TypeTreeNode>(typeTreeNodeCount);
            for (int i = 0; i < typeTreeNodeCount; i++)
            {
                Nodes.Add(new TypeTreeNode(reader, version));
            }
            StringBufferBytes = reader.ReadBytes(stringBufferLen);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType)
                {
                    int dependenciesCount = reader.ReadInt32();
                    TypeDependencies = new int[dependenciesCount];
                    for (int i = 0; i < dependenciesCount; i++)
                    {
                        TypeDependencies[i] = reader.ReadInt32();
                    }
                }
                else
                {
                    TypeReference = new SerializedTypeReference();
                    TypeReference.ReadMetadata(reader);
                }
            }
        }
    }

    public void Write(AssetWriter w, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        w.WriteInt32(TypeID);
        
        if (version >= RefactoredClassId)
        {
            w.WriteBoolean(IsStrippedType);
        }
        
        if (version >= RefactorTypeData)
        {
            w.WriteInt16(ScriptTypeIndex);
        }
        
        if ((version < RefactorTypeData && TypeID < 0) ||
            (version >= RefactorTypeData && TypeID == (int)AssetClassID.MonoBehaviour) ||
            (isRefType && ScriptTypeIndex > 0))
        {
            ScriptIdHash?.Write(w);
        }
        
        TypeHash.Write(w);

        if (typeTreeEnabled)
        {
            w.WriteInt32(Nodes.Count);
            w.WriteInt32(StringBufferBytes.Length);
            foreach (var node in Nodes)
            {
                node.Write(w, version);
            }
            w.Write(StringBufferBytes);
            if (version >= StoresTypeDependencies)
            {
                if (!isRefType)
                {
                    w.WriteInt32(TypeDependencies.Length);
                    foreach (var dep in TypeDependencies)
                    {
                        w.WriteInt32(dep);
                    }
                }
                else
                {
                    TypeReference.Write(w);
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