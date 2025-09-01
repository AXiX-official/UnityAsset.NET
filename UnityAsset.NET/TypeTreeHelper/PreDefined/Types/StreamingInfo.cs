using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class StreamingInfo  : IPreDefinedType
{
    public string ClassName => "StreamingInfo";
    [OriginalName("offset")]
    public UInt64 offset { get; }
    [OriginalName("size")]
    public UInt32 size { get; }
    [OriginalName("path")]
    public string path { get; }
    
    public StreamingInfo(IReader reader)
    {
        offset = reader.ReadUInt64();
        size = reader.ReadUInt32();
        path = reader.ReadSizedString();
        reader.Align(4);
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}UInt64 offset = {offset}");
        sb.AppendLine($"{childIndent}unsigned int size = {size}");
        sb.AppendLine($"{childIndent}string path = \"{path}\"");
        return sb;
    }
}