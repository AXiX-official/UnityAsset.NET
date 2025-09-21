using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Rectf : IPreDefinedType
{
    public string ClassName => "Rectf";
    public float x { get; }
    public float y { get; }
    public float width { get; }
    public float height { get; }

    public Rectf(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        width = reader.ReadFloat();
        height = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {x}");
        sb.AppendLine($"{childIndent}float y = {y}");
        sb.AppendLine($"{childIndent}float width = {width}");
        sb.AppendLine($"{childIndent}float height = {height}");
        return sb;
    }
}