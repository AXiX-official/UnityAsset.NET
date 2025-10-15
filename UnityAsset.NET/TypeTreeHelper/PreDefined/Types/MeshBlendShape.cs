using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class MeshBlendShape : IPreDefinedType
{
    public string ClassName => "MeshBlendShape";
    public UInt32 firstVertex { get; }
    public UInt32 vertexCount { get; }
    public bool hasNormals { get; }
    public bool hasTangents { get; }

    public MeshBlendShape(IReader reader)
    {
        firstVertex = reader.ReadUInt32();
        vertexCount = reader.ReadUInt32();
        hasNormals = reader.ReadBoolean();
        hasTangents = reader.ReadBoolean();
        reader.Align(4);
    }
    
    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        sb.AppendLine($"{childIndent}unsigned int firstVertex = {this.firstVertex}");
        sb.AppendLine($"{childIndent}unsigned int vertexCount = {this.vertexCount}");
        sb.AppendLine($"{childIndent}bool hasNormals = {this.hasNormals}");
        sb.AppendLine($"{childIndent}bool hasTangents = {this.hasTangents}");
        return sb;
    }
}