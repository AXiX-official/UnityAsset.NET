using System.Runtime.InteropServices;
using System.Text;

namespace UnityAsset.NET.Extensions;

public static class ListExtensions
{
    public static string ToPlainText<T>(this List<T> list, string indent = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{indent}    Array Array");
        sb.AppendLine($"{indent}    int size = {list.Count}");
        for (int i = 0; i < list.Count; i++)
        {
            sb.AppendLine($"{indent}        [{i}]");
            
        }
        return sb.ToString();
    }
}