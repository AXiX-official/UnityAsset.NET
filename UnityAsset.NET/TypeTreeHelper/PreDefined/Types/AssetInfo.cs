using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class AssetInfo : IPreDefinedType
{
    public string ClassName => "AssetInfo";
    public Int32 preloadIndex { get; }
    public Int32 preloadSize { get; }
    public PPtr<Object> asset { get; }

    public AssetInfo(IReader reader)
    {
        preloadIndex = reader.ReadInt32();
        preloadSize = reader.ReadInt32();
        asset = new PPtr<Object>(reader);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}int preloadIndex = {preloadIndex}");
        sb.AppendLine($"{childIndent}int preloadSize = {preloadSize}");
        asset.ToPlainText("asset", sb, childIndent);
        return sb;
    }
}