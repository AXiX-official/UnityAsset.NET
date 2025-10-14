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

    public Vector2f(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {x}");
        sb.AppendLine($"{childIndent}float y = {y}");
        return sb;
    }
    
    public static explicit operator Vector2f(Vector3f v)
    {
        return new Vector2f(v.x, v.y);
    }
}