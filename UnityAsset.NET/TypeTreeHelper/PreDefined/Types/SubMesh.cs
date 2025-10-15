using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SubMesh  : IPreDefinedType
{
    public string ClassName => "SubMesh";
    
    public UInt32 firstByte { get; }
    public UInt32 indexCount { get; }
    public GfxPrimitiveType topology { get; }
    public UInt32 baseVertex { get; }
    public UInt32 firstVertex { get; }
    public UInt32 vertexCount { get; }
    public AABB localAABB { get; }
    
    public SubMesh(IReader reader)
    {
        firstByte = reader.ReadUInt32();
        indexCount = reader.ReadUInt32();
        topology = (GfxPrimitiveType)reader.ReadInt32();
        baseVertex = reader.ReadUInt32();
        firstVertex = reader.ReadUInt32();
        vertexCount = reader.ReadUInt32();
        localAABB = new AABB(reader);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        sb.AppendLine($"{childIndent}unsigned int firstByte = {firstByte}");
        sb.AppendLine($"{childIndent}unsigned int indexCount = {indexCount}");
        sb.AppendLine($"{childIndent}int topology = {topology}");
        sb.AppendLine($"{childIndent}unsigned int baseVertex = {baseVertex}");
        sb.AppendLine($"{childIndent}unsigned int firstVertex = {firstVertex}");
        sb.AppendLine($"{childIndent}unsigned int vertexCount = {vertexCount}");
        sb.AppendLine($"{childIndent}AABB localAABB");
        localAABB.ToPlainText("localAABB", sb, childIndent);
        return sb;
    }
}