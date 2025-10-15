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

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        vertex.ToPlainText("vertex", sb, childIndent);
        normal.ToPlainText("normal", sb, childIndent);
        tangent.ToPlainText("tangent", sb, childIndent);
        sb.AppendLine($"{childIndent}unsigned int index = {index}");
        return sb;
    }
}