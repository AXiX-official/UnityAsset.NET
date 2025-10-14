using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class BuildTextureStackReference : IPreDefinedType
{
    public string ClassName => "BuildTextureStackReference";

    public string groupName { get; }

    public string itemName { get; }

    public BuildTextureStackReference(IReader reader)
    {
        groupName = reader.ReadSizedString();
        reader.Align(4);
        itemName = reader.ReadSizedString();
        reader.Align(4);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}string groupName = \"{this.groupName}\"");
        sb.AppendLine($"{childIndent}string itemName = \"{this.itemName}\"");
        return sb;
    }
}