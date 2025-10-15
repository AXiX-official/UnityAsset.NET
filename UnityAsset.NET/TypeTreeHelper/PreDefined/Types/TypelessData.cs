using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class TypelessData  : IPreDefinedType
{
    public Int32 size { get; }
    public byte[] data { get; }
    
    public TypelessData(IReader reader)
    {
        size = reader.ReadInt32();
        data = reader.ReadBytes(size);
    }
    
    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        sb.AppendLine($"{indent}\tint size = {size}");
        sb.AppendLine($"{indent}\tvector data");
        sb.AppendLine($"{indent}\t\tArray Array");
        sb.AppendLine($"{indent}\t\tint size = {data.Length}");
        for (int i = 0; i < data.Length; i++)
        {
            sb.AppendLine($"{indent}\t\t\t[{i}]");
            sb.AppendLine($"{indent}\t\t\tbyte = {data[i]}");
        }
        return sb;
    }

    public string ClassName => "TypelessData";
}