using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;


namespace UnityAsset.NET.SerializedFiles;

public sealed class SerializedFileMetadata
{
    public string UnityVersion;
    public BuildTarget TargetPlatform;
    public bool TypeTreeEnabled;
    public List<SerializedType> Types;
    public List<AssetFileInfo> AssetInfos;
    public List<AssetPPtr> ScriptTypes;
    public List<AssetsFileExternal> Externals;
    public List<SerializedType>? RefTypes;
    public string UserInformation;
    
    public SerializedFileMetadata(string unityVersion, BuildTarget targetPlatform, bool typeTreeEnabled,
        List<SerializedType> types, List<AssetFileInfo> assetInfos, List<AssetPPtr> scriptTypes,
        List<AssetsFileExternal> externals, List<SerializedType>? refTypes, string userInformation)
    {
        UnityVersion = unityVersion;
        TargetPlatform = targetPlatform;
        TypeTreeEnabled = typeTreeEnabled;
        Types = types;
        AssetInfos = assetInfos;
        ScriptTypes = scriptTypes;
        Externals = externals;
        RefTypes = refTypes;
        UserInformation = userInformation;
    }
    
    public static SerializedFileMetadata ParseFromReader(AssetReader reader, SerializedFileFormatVersion version)
    {
        var unityVersion = reader.ReadStringToNull();
        var targetPlatform = (BuildTarget)reader.ReadUInt32();
        var typeTreeEnabled = version >= SerializedFileFormatVersion.HasTypeTreeHashes && reader.ReadBoolean();
        
        var types = reader.ReadList<SerializedType>(reader.ReadInt32(), r => SerializedType.ParseFromReader(r, version, typeTreeEnabled, false));
        
        int assetCount = reader.ReadInt32();
        reader.Align(4);
        var assetInfos = reader.ReadList<AssetFileInfo>(assetCount, r => AssetFileInfo.ParseFromReader(r, version));
        foreach (var assetInfo in assetInfos)
        {
            assetInfo.TypeId = assetInfo.GetTypeId(types, version);
        }
        
        var scriptTypes = reader.ReadList(reader.ReadInt32(), AssetPPtr.ParseFromReader);
        
        var externals = reader.ReadList(reader.ReadInt32(), AssetsFileExternal.ParseFromReader);
    
        List<SerializedType>? refTypes = version >= SerializedFileFormatVersion.SupportsRefObject ?
            reader.ReadList<SerializedType>(reader.ReadInt32(), r => SerializedType.ParseFromReader(r, version, typeTreeEnabled, true)) :
            null;
        
        var userInformation = reader.ReadStringToNull();
        return new SerializedFileMetadata(unityVersion, targetPlatform, typeTreeEnabled, types, assetInfos, scriptTypes, externals, refTypes, userInformation);
    }

    public void Serialize(AssetWriter writer, SerializedFileFormatVersion version)
    {
        writer.WriteStringToNull(UnityVersion);
        writer.WriteUInt32((UInt32)TargetPlatform);
        if (version >= SerializedFileFormatVersion.HasTypeTreeHashes)
        {
            writer.WriteBoolean(TypeTreeEnabled);
        }
        writer.WriteListWithCount(Types, (w, type) => type.Serialize(w, version, TypeTreeEnabled, false));
        writer.WriteInt32(AssetInfos.Count);
        writer.Align(4);
        writer.WriteList(AssetInfos, (w, assetInfo) => assetInfo.Serialize(w, version));
        writer.WriteListWithCount(ScriptTypes, (w, assetPPtr) => assetPPtr.Serialize(w));
        writer.WriteListWithCount(Externals, (w, external) => external.Serialize(w));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            if (RefTypes == null)
            {
                throw new NullReferenceException("RefTypes is null");
            }
            writer.WriteListWithCount(RefTypes, (w, type) => type.Serialize(w, version, TypeTreeEnabled, true));
        }
        writer.WriteStringToNull(UserInformation);
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