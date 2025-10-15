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

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        sb.AppendLine($"{childIndent}Vector3f m_Center");
        m_Center.ToPlainText("m_Center", sb, childIndent);
        sb.AppendLine($"{childIndent}Vector3f m_Extent");
        m_Extent.ToPlainText("m_Extent", sb, childIndent);
        return sb;
    }
}