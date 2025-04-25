using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

public class AssetsFileExternal
{
    public string VirtualAssetPathName;

    public GUID128 Guid;

    public AssetsFileExternalType Type;

    public string PathName;

    public string OriginalPathName;

    public AssetsFileExternal(AssetReader r)
    {
        VirtualAssetPathName = r.ReadNullTerminated();
        Guid = new GUID128(r);
        Type = (AssetsFileExternalType)r.ReadInt32();
        PathName = r.ReadNullTerminated();
        OriginalPathName = PathName;
        
        // workaround from https://github.com/nesrak1/AssetsTools.NET/blob/main/AssetTools.NET/Standard/AssetsFileFormat/AssetsFileExternal.cs#L51
        if (PathName == "resources/unity_builtin_extra")
        {
            PathName = "Resources/unity_builtin_extra";
        }
        else if (PathName == "library/unity default resources" || PathName == "Library/unity default resources")
        {
            PathName = "Resources/unity default resources";
        }
        else if (PathName == "library/unity editor resources" || PathName == "Library/unity editor resources")
        {
            PathName = "Resources/unity editor resources";
        }
    }

    public void Write(AssetWriter w)
    {
        w.WriteStringToNull(VirtualAssetPathName);
        Guid.Write(w);
        w.WriteInt32((Int32)Type);
        var ToWritePathName = PathName;
        if ((PathName == "Resources/unity_builtin_extra" ||
             PathName == "Resources/unity default resources" ||
             PathName == "Resources/unity editor resources")
            && OriginalPathName != string.Empty)
        {
            ToWritePathName = OriginalPathName;
        }
        w.WriteStringToNull(ToWritePathName);
    }

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