using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Quaternionf : IPreDefinedType
{
    public string ClassName => "Quaternionf";

    public float x { get; }

    public float y { get; }

    public float z { get; }

    public float w { get; }

    public Quaternionf(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
        w = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {this.x}");
        sb.AppendLine($"{childIndent}float y = {this.y}");
        sb.AppendLine($"{childIndent}float z = {this.z}");
        sb.AppendLine($"{childIndent}float w = {this.w}");
        return sb;
    }
}