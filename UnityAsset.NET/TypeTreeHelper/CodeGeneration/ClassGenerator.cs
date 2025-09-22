using System.Text;

namespace UnityAsset.NET.TypeTreeHelper.CodeGeneration;

public static class ClassGenerator
{
    public static void Generate(ComplexTypeInfo type, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();

        sb.AppendLine();
        sb.AppendLine($"[OriginalName(\"{type.OriginalName}\")]");
        sb.AppendLine($"public class {type.ConcreteName} : {type.InterfaceName}");
        sb.AppendLine("{");

        sb.AppendLine($"    public string ClassName => \"{type.OriginalName}\";");
        
        GenerateFields(type.Fields, sb);
        GenerateConstructor(type.ConcreteName, type.Fields, sb);
        GenerateToPlainTextMethod(type, sb);
        
        sb.AppendLine("}");
    }

    private static void GenerateFields((string, BaseTypeInfo)[] fields, StringBuilder sb)
    {
        foreach (var (fieldName, fieldInfo) in fields)
        {
            sb.AppendLine($"    [OriginalName(\"{fieldInfo.OriginalName}\")]");
            sb.AppendLine($"    public {fieldInfo.DeclarationName} {fieldName} {{ get; }}");
        }
    }

    private static void GenerateConstructor(string className, (string, BaseTypeInfo)[] fields, StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine($"    public {className}(IReader reader)");
        sb.AppendLine($"    {{");

        foreach (var (fieldName, fieldInfo) in fields)
        {
            sb.AppendLine($"        {fieldName} = {fieldInfo.ReadLogic};");
            
            if (fieldInfo.RequiresAlign)
            {
                sb.AppendLine("        ((IReader)reader).Align(4);");
            }
        }

        sb.AppendLine($"    }}");
    }

    private static void GenerateToPlainTextMethod(ComplexTypeInfo type, StringBuilder sb, string indent = "    ")
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}public StringBuilder ToPlainText(string name = \"Base\", StringBuilder? sb = null, string indent = \"\")");
        sb.AppendLine($"{indent}{{");
        indent += "    ";
        sb.AppendLine($"{indent}sb ??= new StringBuilder();");
        sb.AppendLine($"{indent}sb.AppendLine($\"{{indent}}{{ClassName}} {{name}}\");");
        sb.AppendLine($"{indent}var childIndent = indent + \"    \";");

        foreach (var (fieldName, fieldInfo) in type.Fields)
        {
            GetToPlainTextRecursive(fieldName, fieldInfo, sb, indent, fieldName);
        }
        
        sb.AppendLine("        return sb;");
        sb.AppendLine("    }");
    }

    private static void GetToPlainTextRecursive(string fieldName, BaseTypeInfo typeInfo, StringBuilder sb, string indent, string valueName)
    {
        switch (typeInfo)
        {
            case PrimitiveTypeInfo primitiveType:
            {
                if (primitiveType.OriginalName == "string")
                {
                    sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{primitiveType.OriginalName} {valueName} \\\"{{{valueName}}}\\\"\");");
                }
                else
                {
                    sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{primitiveType.OriginalName} {valueName} {{{valueName}}}\");");
                }
                break;
            }
            case VectorTypeInfo vectorTypeInfo:
            {
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{vectorTypeInfo.OriginalName} {valueName}\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    {vectorTypeInfo.ArrayType} {vectorTypeInfo.ArrayName}\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    {vectorTypeInfo.SizeType} {vectorTypeInfo.SizeName} = {{{valueName}.Count}}\");");
                sb.AppendLine($"{indent}for (int {fieldName}Count = 0; {fieldName}Count < {valueName}.Count; {fieldName}Count++)");
                sb.AppendLine($"{indent}{{");
                var identBackup = indent;
                indent += "    ";
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}        [{{{fieldName}Count}}]\");");
                sb.AppendLine($"{indent}var {fieldName}childIndentBackUp = childIndent;");
                sb.AppendLine($"{indent}childIndent = $\"{{childIndent}}        \";");
                GetToPlainTextRecursive(vectorTypeInfo.ElementName, vectorTypeInfo.ElementTypeInfo, sb, indent, $"{valueName}[{fieldName}Count]");
                sb.AppendLine($"{indent}childIndent = {fieldName}childIndentBackUp;");
                indent = identBackup;
                sb.AppendLine($"{indent}}}");
                break;
            }
            case MapTypeInfo mapTypeInfo:
            {
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{mapTypeInfo.OriginalName} {fieldName}\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    Array Array\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    int size = {{{valueName}.Count}}\");");
                sb.AppendLine($"{indent}foreach (var {fieldName}Count = 0; {fieldName}Count < {fieldName}.Count; {fieldName}Count++)");
                var identBackup = indent;
                indent += "    ";
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}        [{{{fieldName}Count}}]\");");
                sb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}        {mapTypeInfo.PairType} {mapTypeInfo.PairName}\");");
                GetToPlainTextRecursive(mapTypeInfo.KeyName, mapTypeInfo.KeyTypeInfo, sb, indent, $"{valueName}[{fieldName}Count].Key");
                GetToPlainTextRecursive(mapTypeInfo.ValueName, mapTypeInfo.ValueTypeInfo, sb, indent, $"{valueName}[{fieldName}Count].Value");
                indent = identBackup;
                sb.AppendLine($"{indent}}}");
                break;
            }
            case PredefinedTypeInfo:
            case ComplexTypeInfo:
            {
                sb.AppendLine($"{indent}{valueName}.ToPlainText(\"{fieldName}\", sb, childIndent);");
                break;
            }
        }
    }
}
