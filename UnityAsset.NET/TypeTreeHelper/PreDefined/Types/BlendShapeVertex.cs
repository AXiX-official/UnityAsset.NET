using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class BlendShapeVertex : IPreDefinedType
{
    public string ClassName => "BlendShapeVertex";
    public Vector3f vertex { get; }
    public Vector3f normal { get; }
    public Vector3f tangent { get; }
    public UInt32 index { get; }

    public BlendShapeVertex(IReader reader)
    {
        vertex = new Vector3f(reader);
        normal = new Vector3f(reader);
        tangent = new Vector3f(reader);
        index = reader.ReadUInt32();
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}Vector3f vertex = {vertex}");
        sb.AppendLine($"{childIndent}Vector3f normal = {normal}");
        sb.AppendLine($"{childIndent}Vector3f tangent = {tangent}");
        sb.AppendLine($"{childIndent}unsigned int index = {index}");
        return sb;
    }
}