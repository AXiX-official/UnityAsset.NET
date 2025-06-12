using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public class AssetsFileExternal
{
    public string VirtualAssetPathName;
    public GUID128 Guid;
    public AssetsFileExternalType Type;
    public string PathName;
    public string OriginalPathName;

    public AssetsFileExternal(string virtualAssetPathName, GUID128 guid, AssetsFileExternalType type, string pathName,
        string originalPathName)
    {
        VirtualAssetPathName = virtualAssetPathName;
        Guid = guid;
        Type = type;
        PathName = pathName;
        OriginalPathName = originalPathName;
    }

    public static AssetsFileExternal Parse(ref DataBuffer db)
    {
        var virtualAssetPathName = db.ReadNullTerminatedString();
        var guid = new GUID128(ref db);
        var type = (AssetsFileExternalType)db.ReadInt32();
        var pathName = db.ReadNullTerminatedString();
        var originalPathName = pathName;
        
        // workaround from https://github.com/nesrak1/AssetsTools.NET/blob/main/AssetTools.NET/Standard/AssetsFileFormat/AssetsFileExternal.cs#L51
        if (pathName == "resources/unity_builtin_extra")
        {
            pathName = "Resources/unity_builtin_extra";
        }
        else if (pathName == "library/unity default resources" || pathName == "Library/unity default resources")
        {
            pathName = "Resources/unity default resources";
        }
        else if (pathName == "library/unity editor resources" || pathName == "Library/unity editor resources")
        {
            pathName = "Resources/unity editor resources";
        }
        return new AssetsFileExternal(virtualAssetPathName, guid, type, pathName, originalPathName);
    }

    public void Serialize(ref DataBuffer db)
    {
        db.WriteNullTerminatedString(VirtualAssetPathName);
        Guid.Serialize(ref db);
        db.WriteInt32((Int32)Type);
        var toWritePathName = PathName;
        if ((PathName == "Resources/unity_builtin_extra" ||
             PathName == "Resources/unity default resources" ||
             PathName == "Resources/unity editor resources")
            && OriginalPathName != string.Empty)
        {
            toWritePathName = OriginalPathName;
        }
        db.WriteNullTerminatedString(toWritePathName);
    }
    
    public long SerializeSize => 22 + VirtualAssetPathName.Length + Math.Max(OriginalPathName.Length, PathName.Length);

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("AssetsFileExternal: ");
        sb.AppendLine($"VirtualAssetPathName: {VirtualAssetPathName} | ");
        sb.AppendLine($"GUID: {Guid} | ");
        sb.AppendLine($"Type: {Type} | ");
        sb.AppendLine($"PathName: {PathName} | ");
        sb.AppendLine($"OriginalPathName: {OriginalPathName} | ");
        return sb.ToString();
    }
}