using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class VariableBoneCountWeights : IPreDefinedType
{
    public string ClassName => "VariableBoneCountWeights";

    public List<UInt32> m_Data { get; }

    public VariableBoneCountWeights(IReader reader)
    {
        m_Data = reader.ReadListWithAlign<UInt32>(reader.ReadInt32(), r => r.ReadUInt32(), false);
        reader.Align(4);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}vector m_Data");
        sb.AppendLine($"{childIndent}    Array Array");
        sb.AppendLine($"{childIndent}    int size =  {(uint)m_Data.Count}");
        for (int im_Data = 0; im_Data < m_Data.Count; im_Data++)
        {
            var m_DatachildIndentBackUp = childIndent;
            childIndent = $"{childIndent}        ";
            sb.AppendLine($"{childIndent}[{im_Data}]");
            sb.AppendLine($"{childIndent}unsigned int data = {m_Data[im_Data]}");
            childIndent = m_DatachildIndentBackUp;
        }
        return sb;
    }
}