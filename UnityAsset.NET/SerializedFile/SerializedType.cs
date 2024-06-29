using static UnityAsset.NET.Enums.SerializedFileFormatVersion;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedType
{
    public int ClassID;
    
    public bool IsStrippedType;
    
    public short ScriptTypeIndex;

    public Hash128 ScriptIdHash;
    
    public Hash128 TypeHash;
    
    public bool IsRefType;
    
    public List<SerializedType> Nodes;
    
    public byte[] StringBufferBytes;

    public int[] TypeDependencies;

    public SerializedTypeReference TypeReference;
    
    public SerializedType(AssetReader reader, SerializedFileFormatVersion version, bool typeTreeEnabled, bool isRefType)
    {
        ClassID = reader.ReadInt32();
        
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
        
        if ((version < RefactorTypeData && ClassID < 0) ||
            (version >= RefactorTypeData && ClassID == (int)AssetClassID.MonoBehaviour) ||
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
            Nodes = new List<SerializedType>(typeTreeNodeCount);
            for (int i = 0; i < typeTreeNodeCount; i++)
            {
                Nodes.Add(new SerializedType(reader, version, typeTreeEnabled, false));
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
}