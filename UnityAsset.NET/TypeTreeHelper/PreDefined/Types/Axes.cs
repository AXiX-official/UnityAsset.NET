using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Axes : IPreDefinedType
{
    public string ClassName => "Axes";

    public float4 m_PreQ { get; }

    public float4 m_PostQ { get; }

    public float3 m_Sgn { get; }

    public Limit m_Limit { get; }

    public float m_Length { get; }

    public UInt32 m_Type { get; }

    public Axes(IReader reader)
    {
        m_PreQ = new float4(reader);
        m_PostQ = new float4(reader);
        m_Sgn = new float3(reader);
        m_Limit = new Limit(reader);
        m_Length = reader.ReadFloat();
        m_Type = reader.ReadUInt32();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        this.m_PreQ?.ToPlainText("m_PreQ", sb, childIndent);
        this.m_PostQ?.ToPlainText("m_PostQ", sb, childIndent);
        this.m_Sgn?.ToPlainText("m_Sgn", sb, childIndent);
        this.m_Limit?.ToPlainText("m_Limit", sb, childIndent);
        sb.AppendLine($"{childIndent}float m_Length = {this.m_Length}");
        sb.AppendLine($"{childIndent}unsigned int m_Type = {this.m_Type}");
        return sb;
    }
}