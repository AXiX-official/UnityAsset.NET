using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SecondarySpriteTexture : IPreDefinedType
{
    public string ClassName => "SecondarySpriteTexture";
    public PPtr<ITexture2D> texture { get; }
    public string name { get; }

    public SecondarySpriteTexture(IReader reader)
    {
        texture = new PPtr<ITexture2D>(reader);
        name = reader.ReadSizedString();
        reader.Align(4);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        texture.ToPlainText("texture", sb, childIndent);
        sb.AppendLine($"{childIndent}string name \"{name}\"");
        return sb;
    }
}