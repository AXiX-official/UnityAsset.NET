using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

public sealed class SerializedFileMetadata
{
    public string UnityVersion;
    public BuildTarget TargetPlatform;
    public bool TypeTreeEnabled;
    public SerializedType[] Types;
    public AssetFileInfo[] AssetInfos;
    public AssetPPtr[] ScriptTypes;
    public AssetsFileExternal[] Externals;
    public SerializedType[]? RefTypes;
    public string UserInformation;
    
    public SerializedFileMetadata(string unityVersion, BuildTarget targetPlatform, bool typeTreeEnabled,
        SerializedType[] types, AssetFileInfo[] assetInfos, AssetPPtr[] scriptTypes,
        AssetsFileExternal[] externals,SerializedType[]? refTypes, string userInformation)
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
        var types = reader.ReadArray(
            r => SerializedType.Parse(r, version, typeTreeEnabled, false)
            );
        int assetCount = reader.ReadInt32();
        reader.Align(4);
        var assetInfos = reader.ReadArray(
            assetCount, 
            r => AssetFileInfo.Parse(r, version, types)
            );
        var scriptTypes = reader.ReadArray(AssetPPtr.Parse);
        var externals = reader.ReadArray(AssetsFileExternal.Parse);
        SerializedType[]? refTypes = version >= SerializedFileFormatVersion.SupportsRefObject ?
            reader.ReadArray(r => SerializedType.Parse(r, version, typeTreeEnabled, true)) :
            null;
        var userInformation = reader.ReadNullTerminatedString();
        return new SerializedFileMetadata(unityVersion, targetPlatform, typeTreeEnabled, types, assetInfos, scriptTypes, externals, refTypes, userInformation);
    }

    public void Serialize(IWriter writer, SerializedFileFormatVersion version)
    {
        writer.WriteNullTerminatedString(UnityVersion);
        writer.WriteUInt32((UInt32)TargetPlatform);
        writer.WriteBoolean(TypeTreeEnabled);
        writer.WriteArrayWithCount(Types, (d, type) => type.Serialize(d, version, TypeTreeEnabled, false));
        writer.WriteInt32(AssetInfos.Length);
        writer.Align(4);
        writer.WriteArray(AssetInfos, (d, assetInfo) => assetInfo.Serialize(d, version));
        writer.WriteArrayWithCount(ScriptTypes, (d, assetPPtr) => assetPPtr.Serialize(d));
        writer.WriteArrayWithCount(Externals, (d, external) => external.Serialize(d));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            if (RefTypes == null)
                throw new NullReferenceException("RefTypes is null");
            writer.WriteArrayWithCount(RefTypes, (d, type) => type.Serialize(d, version, TypeTreeEnabled, true));
        }
        writer.WriteNullTerminatedString(UserInformation);
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("UnityVersion: {0} | ", UnityVersion);
        sb.AppendFormat("Platform: {0} | ", TargetPlatform);
        sb.AppendFormat("TypeTreeEnabled: {0} | ", TypeTreeEnabled);
        sb.AppendFormat("Types: {0} | ", Types.Length);
        sb.AppendFormat("AssetInfos: {0} | ", AssetInfos.Length);
        sb.AppendFormat("ScriptTypes: {0} | ", ScriptTypes.Length);
        sb.AppendFormat("Externals: {0} | ", Externals.Length);
        sb.AppendFormat("RefTypes: {0} | ", RefTypes?.Length ?? 0);
        sb.AppendFormat("UserInformation: {0}", UserInformation);
        return sb.ToString();
    }
}