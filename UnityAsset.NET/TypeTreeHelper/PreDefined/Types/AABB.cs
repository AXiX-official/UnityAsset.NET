using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class AABB : IPreDefinedType
{
    public string ClassName => "AABB";
    public Vector3f m_Center { get; }
    public Vector3f m_Extent { get; }

    public AABB(IReader reader)
    {
        m_Center = new Vector3f(reader);
        m_Extent = new Vector3f(reader);
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}Vector3f m_Center");
        m_Center.ToPlainText(sb, childIndent);
        sb.AppendLine($"{childIndent}Vector3f m_Extent");
        m_Extent.ToPlainText(sb, childIndent);
        return sb;
    }
}