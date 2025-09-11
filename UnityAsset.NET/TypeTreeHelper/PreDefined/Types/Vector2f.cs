using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Vector2f : IPreDefinedType
{
    public string ClassName => "Vector2f";
    public float x { get; }
    public float y { get; }

    public Vector2f(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {x}");
        sb.AppendLine($"{childIndent}float y = {y}");
        return sb;
    }
}