using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class GUID : IPreDefinedType
{
    public string ClassName => "GUID";
    [OriginalName("data[0]")]
    public UInt32 data_0_ { get; }
    [OriginalName("data[1]")]
    public UInt32 data_1_ { get; }
    [OriginalName("data[2]")]
    public UInt32 data_2_ { get; }
    [OriginalName("data[3]")]
    public UInt32 data_3_ { get; }

    public GUID(IReader reader)
    {
        data_0_ = reader.ReadUInt32();
        data_1_ = reader.ReadUInt32();
        data_2_ = reader.ReadUInt32();
        data_3_ = reader.ReadUInt32();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}unsigned int data[0] = {data_0_}");
        sb.AppendLine($"{childIndent}unsigned int data[1] = {data_1_}");
        sb.AppendLine($"{childIndent}unsigned int data[2] = {data_2_}");
        sb.AppendLine($"{childIndent}unsigned int data[3] = {data_3_}");
        return sb;
    }
}