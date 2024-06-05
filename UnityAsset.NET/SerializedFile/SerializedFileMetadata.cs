using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;


namespace UnityAsset.NET.SerializedFile;

public class SerializedFileMetadata
{
    public string UnityVersion;
    
    public BuildTarget Platform;
    
    public bool TypeTreeEnabled;

    public List<SerializedType> Types;
    
    public SerializedFileMetadata(AssetReader reader, SerializedFileFormatVersion version)
    {
        UnityVersion = reader.ReadStringToNull();
        Platform = (BuildTarget)reader.ReadUInt32();
        if (version >= SerializedFileFormatVersion.HasTypeTreeHashes)
        {
            TypeTreeEnabled = reader.ReadBoolean();
        }
        
        int typeCount = reader.ReadInt32();
        Types = new List<SerializedType>(typeCount);
        for (int i = 0; i < typeCount; i++)
        {
            Types.Add(new SerializedType(reader, version, TypeTreeEnabled, false));
        }
    }
}