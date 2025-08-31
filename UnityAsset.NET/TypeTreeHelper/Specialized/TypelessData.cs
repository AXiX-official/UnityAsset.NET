using System.Text;
using UnityAsset.NET.Classes;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper.Specialized;

public class TypelessData : IAsset
{
    public Int32 size { get; }
    public byte[] data { get; }
    
    public TypelessData(IReader reader)
    {
        size = reader.ReadInt32();
        data = reader.ReadBytes(size);
    }
    
    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}    int size = {size}");
        sb.AppendLine($"{indent}    vector data");
        sb.AppendLine($"{indent}        Array Array");
        sb.AppendLine($"{indent}        int size = {data.Length}");
        for (int i = 0; i < data.Length; i++)
        {
            sb.AppendLine($"{indent}            [{i}]");
            sb.AppendLine($"{indent}            byte = {data[i]}");
        }
        return sb;
    }

    public string ClassName => "TypelessData";
}