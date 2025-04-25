using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;


namespace UnityAsset.NET.SerializedFile;

public sealed class SerializedFileMetadata
{
    public string UnityVersion;
    
    public BuildTarget TargetPlatform;
    
    public bool TypeTreeEnabled;

    public List<SerializedType> Types;
    
    public List<AssetFileInfo> AssetInfos;

    public List<AssetPPtr> ScriptTypes;
    
    public List<AssetsFileExternal> Externals;
    
    public List<SerializedType> RefTypes;

    public string UserInformation;
    
    public SerializedFileMetadata(AssetReader reader, SerializedFileFormatVersion version)
    {
        UnityVersion = reader.ReadNullTerminated();
        TargetPlatform = (BuildTarget)reader.ReadUInt32();
        if (version >= SerializedFileFormatVersion.HasTypeTreeHashes)
        {
            TypeTreeEnabled = reader.ReadBoolean();
        }
        
        Types = reader.ReadArray<SerializedType>(reader.ReadInt32(), r => new SerializedType(r, version, TypeTreeEnabled, false));
        
        int assetCount = reader.ReadInt32();
        reader.Align(4);
        AssetInfos = reader.ReadArray<AssetFileInfo>(assetCount, r => new AssetFileInfo(r, version));
        foreach (var assetInfo in AssetInfos)
        {
            assetInfo.TypeId = assetInfo.GetTypeId(this, version);
        }
        
        ScriptTypes = reader.ReadArray<AssetPPtr>(reader.ReadInt32(), r => new AssetPPtr(r));
        
        Externals = reader.ReadArray<AssetsFileExternal>(reader.ReadInt32(), r => new AssetsFileExternal(r));

        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            RefTypes = reader.ReadArray<SerializedType>(reader.ReadInt32(), r => new SerializedType(r, version, TypeTreeEnabled, true));
        }
        
        UserInformation = reader.ReadNullTerminated();
    }

    public void Write(AssetWriter w, SerializedFileFormatVersion version)
    {
        w.WriteStringToNull(UnityVersion);
        w.WriteUInt32((UInt32)TargetPlatform);
        if (version >= SerializedFileFormatVersion.HasTypeTreeHashes)
        {
            w.WriteBoolean(TypeTreeEnabled);
        }
        w.WriteInt32(Types.Count);
        w.WriteArray<SerializedType>(Types, (writer, type) => type.Write(writer, version, TypeTreeEnabled, false));
        w.WriteInt32(AssetInfos.Count);
        w.Align(4);
        w.WriteArray<AssetFileInfo>(AssetInfos, (writer, assetInfo) => assetInfo.Write(writer, version));
        w.WriteInt32(ScriptTypes.Count);
        w.WriteArray<AssetPPtr>(ScriptTypes, (writer, assetPPtr) => assetPPtr.Write(writer));
        w.WriteInt32(Externals.Count);
        w.WriteArray<AssetsFileExternal>(Externals, (writer, external) => external.Write(writer));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            w.WriteInt32(RefTypes.Count);
            w.WriteArray<SerializedType>(RefTypes, (writer, type) => type.Write(writer, version, TypeTreeEnabled, true));
        }
        w.WriteStringToNull(UserInformation);
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("UnityVersion: {0} | ", UnityVersion);
        sb.AppendFormat("Platform: {0} | ", TargetPlatform);
        sb.AppendFormat("TypeTreeEnabled: {0} | ", TypeTreeEnabled);
        sb.AppendFormat("Types: {0} | ", Types.Count);
        sb.AppendFormat("AssetInfos: {0} | ", AssetInfos.Count);
        sb.AppendFormat("ScriptTypes: {0} | ", ScriptTypes.Count);
        sb.AppendFormat("Externals: {0} | ", Externals.Count);
        sb.AppendFormat("RefTypes: {0} | ", RefTypes?.Count ?? 0);
        sb.AppendFormat("UserInformation: {0}", UserInformation);
        return sb.ToString();
    }
}