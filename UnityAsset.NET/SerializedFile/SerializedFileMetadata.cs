using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;


namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFileMetadata
{
    public string UnityVersion;
    
    public BuildTarget Platform;
    
    public bool TypeTreeEnabled;

    public List<SerializedType> Types;
    
    public SerializedFileMetadata(AssetReader reader, SerializedFileFormatVersion version)
    {
        UnityVersion = reader.ReadNullTerminated();
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
        
        int assetCount = reader.ReadInt32();
        reader.AlignStream(4);
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("UnityVersion: {0} | ", UnityVersion);
        sb.AppendFormat("Platform: {0} | ", Platform);
        sb.AppendFormat("TypeTreeEnabled: {0} | ", TypeTreeEnabled);
        sb.AppendFormat("Types: {0}", Types.Count);
        return sb.ToString();
    }
}