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