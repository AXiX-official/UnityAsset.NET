using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class MeshBlendShapeChannel  : IPreDefinedType
{
    public string ClassName => "MeshBlendShapeChannel";
    public string name { get; }
    public UInt32 nameHash { get; }
    public Int32 frameIndex { get; }
    public Int32 frameCount { get; }

    public MeshBlendShapeChannel(IReader reader)
    {
        name = reader.ReadSizedString();
        reader.Align(4);
        nameHash = reader.ReadUInt32();
        frameIndex = reader.ReadInt32();
        frameCount = reader.ReadInt32();
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}string name = \"{name}\"");
        sb.AppendLine($"{childIndent}unsigned int nameHash = {nameHash}");
        sb.AppendLine($"{childIndent}int frameIndex = {frameIndex}");
        sb.AppendLine($"{childIndent}int frameCount = {frameCount}");
        return sb;
    }
}