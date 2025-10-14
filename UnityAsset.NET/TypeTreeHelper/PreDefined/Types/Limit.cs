using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Limit : IPreDefinedType
{
    public string ClassName => "Limit";

    public float3 m_Min { get; }

    public float3 m_Max { get; }

    public Limit(IReader reader)
    {
        m_Min = new float3(reader);
        m_Max = new float3(reader);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        this.m_Min?.ToPlainText("m_Min", sb, childIndent);
        this.m_Max?.ToPlainText("m_Max", sb, childIndent);
        return sb;
    }
}