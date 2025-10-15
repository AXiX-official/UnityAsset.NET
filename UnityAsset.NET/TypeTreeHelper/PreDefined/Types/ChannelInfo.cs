using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class ChannelInfo : IPreDefinedType
{
    public string ClassName => "ChannelInfo";
    public byte stream { get; }
    public byte offset { get; }
    public byte format { get; }
    public byte dimension { get; }

    public ChannelInfo(IReader reader)
    {
        stream = reader.ReadUInt8();
        offset = reader.ReadUInt8();
        format = reader.ReadUInt8();
        // dimension = (byte)(reader.ReadByte() & 0xF);
        dimension = reader.ReadUInt8();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        sb.AppendLine($"{childIndent}UInt8 stream = {stream}");
        sb.AppendLine($"{childIndent}UInt8 offset = {offset}");
        sb.AppendLine($"{childIndent}UInt8 format = {format}");
        sb.AppendLine($"{childIndent}UInt8 dimension = {dimension}");
        return sb;
    }
}