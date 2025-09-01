using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class MinMaxAABB  : IPreDefinedType
{
    public string ClassName => "MinMaxAABB";
    public Vector3f m_Min { get; }
    public Vector3f m_Max { get; }

    public MinMaxAABB(IReader reader)
    {
        m_Min = new Vector3f(reader);
        m_Max = new Vector3f(reader);
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}Vector3f m_Min = {m_Min}");
        sb.AppendLine($"{childIndent}Vector3f m_Max = {m_Max}");
        return sb;
    }
}