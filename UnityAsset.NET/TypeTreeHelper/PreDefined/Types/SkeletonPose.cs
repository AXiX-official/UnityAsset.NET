using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SkeletonPose : IPreDefinedType
{
    public string ClassName => "SkeletonPose";

    public List<xform> m_X { get; }

    public SkeletonPose(IReader reader)
    {
        m_X = reader.ReadListWithAlign<xform>(reader.ReadInt32(), r => new xform(r), false);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}vector m_X");
        sb.AppendLine($"{childIndent}    Array Array");
        sb.AppendLine($"{childIndent}    int size =  {(uint)this.m_X.Count}");
        for (int im_X = 0; im_X < this.m_X.Count; im_X++)
        {
            var m_XchildIndentBackUp = childIndent;
            childIndent = $"{childIndent}        ";
            sb.AppendLine($"{childIndent}[{im_X}]");
            this.m_X[im_X]?.ToPlainText("data", sb, childIndent);
            childIndent = m_XchildIndentBackUp;
        }
        return sb;
    }
}