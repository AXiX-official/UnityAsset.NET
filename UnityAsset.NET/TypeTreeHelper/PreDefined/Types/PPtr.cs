using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

[OriginalName("PPtr")]
public class PPtr<T> : IPreDefinedType where T : IUnityType
{
    public string ClassName => $"PPtr<{UnityTypeHelper.GetClassName(typeof(T))}>";
    public Int32 m_FileID { get; }
    public Int64 m_PathID { get; }

    public PPtr(IReader reader)
    {
        m_FileID = reader.ReadInt32();
        m_PathID = reader.ReadInt64();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}int m_FileID = {m_FileID}");
        sb.AppendLine($"{childIndent}SInt64 m_PathID = {m_PathID}");
        return sb;
    }
}