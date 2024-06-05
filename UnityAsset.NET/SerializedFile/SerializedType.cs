using static UnityAsset.NET.Enums.SerializedFileFormatVersion;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public class SerializedType
{
    public int ClassID;
    
    public bool IsStrippedType;
    
    public short ScriptTypeIndex;

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
            //ScriptIdHash = new Hash128(reader);
        }
    }
}