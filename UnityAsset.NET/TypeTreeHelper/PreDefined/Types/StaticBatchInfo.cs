using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class StaticBatchInfo : IPreDefinedType
{
    public string ClassName => "StaticBatchInfo";

    public UInt16 firstSubMesh { get; }

    public UInt16 subMeshCount { get; }

    public StaticBatchInfo(IReader reader)
    {
        firstSubMesh = reader.ReadUInt16();
        subMeshCount = reader.ReadUInt16();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}UInt16 firstSubMesh = {this.firstSubMesh}");
        sb.AppendLine($"{childIndent}UInt16 subMeshCount = {this.subMeshCount}");
        return sb;
    }
}