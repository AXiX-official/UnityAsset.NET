using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class xform : IPreDefinedType
{
    public string ClassName => "xform";

    public float3 t { get; }

    public float4 q { get; }

    public float3 s { get; }

    public xform(IReader reader)
    {
        t = new float3(reader);
        q = new float4(reader);
        s = new float3(reader);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        this.t?.ToPlainText("t", sb, childIndent);
        this.q?.ToPlainText("q", sb, childIndent);
        this.s?.ToPlainText("s", sb, childIndent);
        return sb;
    }
}