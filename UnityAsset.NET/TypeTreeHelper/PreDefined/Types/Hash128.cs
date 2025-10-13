using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Hash128 : IPreDefinedType
{
    public string ClassName => "Hash128";

    public byte bytes_0_ { get; }

    public byte bytes_1_ { get; }

    public byte bytes_2_ { get; }

    public byte bytes_3_ { get; }

    public byte bytes_4_ { get; }

    public byte bytes_5_ { get; }

    public byte bytes_6_ { get; }

    public byte bytes_7_ { get; }

    public byte bytes_8_ { get; }

    public byte bytes_9_ { get; }

    public byte bytes_10_ { get; }

    public byte bytes_11_ { get; }

    public byte bytes_12_ { get; }

    public byte bytes_13_ { get; }

    public byte bytes_14_ { get; }

    public byte bytes_15_ { get; }

    public Hash128(IReader reader)
    {
        bytes_0_ = reader.ReadUInt8();
        bytes_1_ = reader.ReadUInt8();
        bytes_2_ = reader.ReadUInt8();
        bytes_3_ = reader.ReadUInt8();
        bytes_4_ = reader.ReadUInt8();
        bytes_5_ = reader.ReadUInt8();
        bytes_6_ = reader.ReadUInt8();
        bytes_7_ = reader.ReadUInt8();
        bytes_8_ = reader.ReadUInt8();
        bytes_9_ = reader.ReadUInt8();
        bytes_10_ = reader.ReadUInt8();
        bytes_11_ = reader.ReadUInt8();
        bytes_12_ = reader.ReadUInt8();
        bytes_13_ = reader.ReadUInt8();
        bytes_14_ = reader.ReadUInt8();
        bytes_15_ = reader.ReadUInt8();
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}UInt8 bytes[0] = {this.bytes_0_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[1] = {this.bytes_1_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[2] = {this.bytes_2_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[3] = {this.bytes_3_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[4] = {this.bytes_4_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[5] = {this.bytes_5_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[6] = {this.bytes_6_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[7] = {this.bytes_7_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[8] = {this.bytes_8_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[9] = {this.bytes_9_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[10] = {this.bytes_10_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[11] = {this.bytes_11_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[12] = {this.bytes_12_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[13] = {this.bytes_13_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[14] = {this.bytes_14_}");
        sb.AppendLine($"{childIndent}UInt8 bytes[15] = {this.bytes_15_}");
        return sb;
    }
}