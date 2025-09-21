using System.Text;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.CodeGeneration;

namespace UnityAsset.NET.TypeTreeHelper;
public class TypeGenerator
{
    public readonly Dictionary<string, List<BaseTypeInfo>> TypeMap = new();

    public string Generate(List<SerializedType> serializedTypes)
    {
        var sb = new StringBuilder();
        TypeMap.Clear();

        var typeCache = TypeCollector.Collect(serializedTypes, TypeMap);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityAsset.NET.IO;");
        sb.AppendLine("using UnityAsset.NET.TypeTreeHelper;");
        sb.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined;");
        sb.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;");
        sb.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;");
        sb.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;");
        sb.AppendLine();
        sb.AppendLine("namespace UnityAsset.NET.RuntimeType;");
        sb.AppendLine();

        foreach (var type in typeCache.Values)
        {
            if (type is ComplexTypeInfo complexTypeInfo)
            {
                ClassGenerator.Generate(complexTypeInfo, sb);
            }
        }

        return sb.ToString();
    }
}
