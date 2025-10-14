using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class float3 : IPreDefinedType
{
    public string ClassName => "float3";

    public float x { get; }

    public float y { get; }

    public float z { get; }

    public float3(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {this.x}");
        sb.AppendLine($"{childIndent}float y = {this.y}");
        sb.AppendLine($"{childIndent}float z = {this.z}");
        return sb;
    }
}