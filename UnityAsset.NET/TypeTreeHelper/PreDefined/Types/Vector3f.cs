using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Vector3f : IPreDefinedType
{
    public string ClassName => "Vector3f";
    public float x { get; }
    public float y { get; }
    public float z { get; }

    public Vector3f(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
    }

    public Vector3f(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        sb.AppendLine($"{childIndent}float x = {x}");
        sb.AppendLine($"{childIndent}float y = {y}");
        sb.AppendLine($"{childIndent}float z = {z}");
        return sb;
    }
}