using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

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
    
    public static SerializedFileMetadata Parse(IReader reader, SerializedFileFormatVersion version)
    {
        var unityVersion = reader.ReadNullTerminatedString();
        var targetPlatform = (BuildTarget)reader.ReadUInt32();
        var typeTreeEnabled = reader.ReadBoolean();
        var types = reader.ReadList(reader.ReadInt32(), r => SerializedType.Parse(r, version, typeTreeEnabled, false));
        int assetCount = reader.ReadInt32();
        reader.Align(4);
        var assetInfos = reader.ReadList(assetCount, r => AssetFileInfo.Parse(r, version, types));
        var scriptTypes = reader.ReadList(reader.ReadInt32(), AssetPPtr.Parse);
        var externals = reader.ReadList(reader.ReadInt32(), AssetsFileExternal.Parse);
        List<SerializedType>? refTypes = version >= SerializedFileFormatVersion.SupportsRefObject ?
            reader.ReadList(reader.ReadInt32(), r => SerializedType.Parse(r, version, typeTreeEnabled, true)) :
            null;
        var userInformation = reader.ReadNullTerminatedString();
        return new SerializedFileMetadata(unityVersion, targetPlatform, typeTreeEnabled, types, assetInfos, scriptTypes, externals, refTypes, userInformation);
    }

    public void Serialize(IWriter writer, SerializedFileFormatVersion version)
    {
        writer.WriteNullTerminatedString(UnityVersion);
        writer.WriteUInt32((UInt32)TargetPlatform);
        writer.WriteBoolean(TypeTreeEnabled);
        writer.WriteListWithCount(Types, (d, type) => type.Serialize(d, version, TypeTreeEnabled, false));
        writer.WriteInt32(AssetInfos.Count);
        writer.Align(4);
        writer.WriteList(AssetInfos, (d, assetInfo) => assetInfo.Serialize(d, version));
        writer.WriteListWithCount(ScriptTypes, (d, assetPPtr) => assetPPtr.Serialize(d));
        writer.WriteListWithCount(Externals, (d, external) => external.Serialize(d));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            if (RefTypes == null)
                throw new NullReferenceException("RefTypes is null");
            writer.WriteListWithCount(RefTypes, (d, type) => type.Serialize(d, version, TypeTreeEnabled, true));
        }
        writer.WriteNullTerminatedString(UserInformation);
    }
    
    public long SerializeSize => UnityVersion.Length + UserInformation.Length + 27 + 
                                 Types.Sum(t => t.SerializeSize) + 
                                 AssetInfos.Sum(a => a.SerializeSize) + 
                                 ScriptTypes.Sum(s => s.SerializeSize) + 
                                 Externals.Sum(e => e.SerializeSize) + 
                                 (RefTypes == null ? 0 : 4 + RefTypes.Sum(r => r.SerializeSize));
    
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