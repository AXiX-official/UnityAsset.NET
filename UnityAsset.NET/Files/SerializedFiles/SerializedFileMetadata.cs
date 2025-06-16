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
    
    public static SerializedFileMetadata Parse(DataBuffer db, SerializedFileFormatVersion version)
    {
        var unityVersion = db.ReadNullTerminatedString();
        var targetPlatform = (BuildTarget)db.ReadUInt32();
        var typeTreeEnabled = db.ReadBoolean();
        var types = db.ReadList(db.ReadInt32(), d => SerializedType.Parse(d, version, typeTreeEnabled, false));
        int assetCount = db.ReadInt32();
        db.Align(4);
        var assetInfos = db.ReadList(assetCount, d => AssetFileInfo.Parse(d, version, types));
        var scriptTypes = db.ReadList(db.ReadInt32(), AssetPPtr.Parse);
        var externals = db.ReadList(db.ReadInt32(), AssetsFileExternal.Parse);
        List<SerializedType>? refTypes = version >= SerializedFileFormatVersion.SupportsRefObject ?
            db.ReadList(db.ReadInt32(), d => SerializedType.Parse(d, version, typeTreeEnabled, true)) :
            null;
        var userInformation = db.ReadNullTerminatedString();
        return new SerializedFileMetadata(unityVersion, targetPlatform, typeTreeEnabled, types, assetInfos, scriptTypes, externals, refTypes, userInformation);
    }

    public int Serialize(DataBuffer db, SerializedFileFormatVersion version)
    {
        int size = 0;
        size += db.WriteNullTerminatedString(UnityVersion);
        size += db.WriteUInt32((UInt32)TargetPlatform);
        size += db.WriteBoolean(TypeTreeEnabled);
        size += db.WriteListWithCount(Types, (d, type) => type.Serialize(d, version, TypeTreeEnabled, false));
        size += db.WriteInt32(AssetInfos.Count);
        size += db.Align(4);
        size += db.WriteList(AssetInfos, (d, assetInfo) => assetInfo.Serialize(d, version));
        size += db.WriteListWithCount(ScriptTypes, (d, assetPPtr) => assetPPtr.Serialize(d));
        size += db.WriteListWithCount(Externals, (d, external) => external.Serialize(d));
        if (version >= SerializedFileFormatVersion.SupportsRefObject)
        {
            if (RefTypes == null)
                throw new NullReferenceException("RefTypes is null");
            size += db.WriteListWithCount(RefTypes, (d, type) => type.Serialize(d, version, TypeTreeEnabled, true));
        }
        size += db.WriteNullTerminatedString(UserInformation);
        return size;
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