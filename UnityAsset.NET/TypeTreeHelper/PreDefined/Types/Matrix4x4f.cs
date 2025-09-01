using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Matrix4x4f : IPreDefinedType
{
    public string ClassName => "Matrix4x4f";
    public float e00 { get; }
    public float e01 { get; }
    public float e02 { get; }
    public float e03 { get; }
    public float e10 { get; }
    public float e11 { get; }
    public float e12 { get; }
    public float e13 { get; }
    public float e20 { get; }
    public float e21 { get; }
    public float e22 { get; }
    public float e23 { get; }
    public float e30 { get; }
    public float e31 { get; }
    public float e32 { get; }
    public float e33 { get; }

    public Matrix4x4f(IReader reader)
    {
        e00 = reader.ReadFloat();
        e01 = reader.ReadFloat();
        e02 = reader.ReadFloat();
        e03 = reader.ReadFloat();
        e10 = reader.ReadFloat();
        e11 = reader.ReadFloat();
        e12 = reader.ReadFloat();
        e13 = reader.ReadFloat();
        e20 = reader.ReadFloat();
        e21 = reader.ReadFloat();
        e22 = reader.ReadFloat();
        e23 = reader.ReadFloat();
        e30 = reader.ReadFloat();
        e31 = reader.ReadFloat();
        e32 = reader.ReadFloat();
        e33 = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float e00 = {e00}");
        sb.AppendLine($"{childIndent}float e01 = {e01}");
        sb.AppendLine($"{childIndent}float e02 = {e02}");
        sb.AppendLine($"{childIndent}float e03 = {e03}");
        sb.AppendLine($"{childIndent}float e10 = {e10}");
        sb.AppendLine($"{childIndent}float e11 = {e11}");
        sb.AppendLine($"{childIndent}float e12 = {e12}");
        sb.AppendLine($"{childIndent}float e13 = {e13}");
        sb.AppendLine($"{childIndent}float e20 = {e20}");
        sb.AppendLine($"{childIndent}float e21 = {e21}");
        sb.AppendLine($"{childIndent}float e22 = {e22}");
        sb.AppendLine($"{childIndent}float e23 = {e23}");
        sb.AppendLine($"{childIndent}float e30 = {e30}");
        sb.AppendLine($"{childIndent}float e31 = {e31}");
        sb.AppendLine($"{childIndent}float e32 = {e32}");
        sb.AppendLine($"{childIndent}float e33 = {e33}");
        return sb;
    }
}