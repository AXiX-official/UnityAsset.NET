using System.Text;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper;

public class TypeGenerator
{
    private readonly StringBuilder sb = new();
    private Dictionary<long, string> _hashToClassNameMap = new();
    private Queue<TypeTreeNode> _classToGen = new();

    public void CleanCache()
    {
        _hashToClassNameMap.Clear();
    }
    
    public string Generate(List<TypeTreeNode> nodes)
    {
        sb.Clear();
        _classToGen.Clear();

        _classToGen.Enqueue(nodes[0]);

        while (_classToGen.Count > 0)
        {
            GenerateClass(_classToGen.Dequeue(), nodes);
        }
        
        return sb.ToString();
    }

    private void GenerateClass(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        var hash64 = current.GetHash64Code(nodes);
        if (_hashToClassNameMap.ContainsKey(hash64))
            return;
        
        string className = $"{SanitizeName($"{current.Type}_{hash64}")}";
        _hashToClassNameMap[hash64] = className;
        
        sb.AppendLine();
        var children = current.Children(nodes);

        sb.AppendLine($"[OriginalName(\"{current.Type}\")]");
        bool hasNameField = children.Any(node => node.Name == "m_Name");
        string interfaceName = hasNameField ? "INamedAsset" : "IAsset";
        sb.AppendLine($"public class {className} : {interfaceName}");
        sb.AppendLine($"{{");

        sb.AppendLine($"    public string ClassName => \"{current.Type}\";");

        foreach (var fieldNode in children)
        {
            sb.AppendLine($"    [OriginalName(\"{fieldNode.Name}\")]");
            if (!IsPrimitive(fieldNode.Type))
            {
                var filedHash64 = fieldNode.GetHash64Code(nodes);
                if (_hashToClassNameMap.TryGetValue(filedHash64, out var filedName))
                {
                    sb.AppendLine($"    public {filedName} {SanitizeName(fieldNode.Name)} {{ get; }}");
                }
                else
                {
                    sb.AppendLine($"    public {SanitizeName($"{fieldNode.Type}_{filedHash64}")} {SanitizeName(fieldNode.Name)} {{ get; }}");
                    _classToGen.Enqueue(fieldNode);
                }
            }
            else
            {
                sb.AppendLine($"    public {GetCSharpType(fieldNode, nodes)} {SanitizeName(fieldNode.Name)} {{ get; }}");
            }
        }

        GenerateConstructor(className, children, nodes);
        GenerateToPlainTextMethod(current, children, nodes);
        
        foreach (var fieldNode in children)
        {
            if (fieldNode.Type == "vector")
            {
                var dataNode = fieldNode.Children(nodes)[0].Children(nodes)[1];
                if (!IsPrimitive(dataNode.Type) && !_hashToClassNameMap.ContainsKey(dataNode.GetHash64Code(nodes)))
                {
                    _classToGen.Enqueue(dataNode);
                }
                // ugly patch
                else if (dataNode.Type == "vector")
                {
                    var subDataNode = dataNode.Children(nodes)[0].Children(nodes)[1];
                    if (!IsPrimitive(subDataNode.Type) && !_hashToClassNameMap.ContainsKey(subDataNode.GetHash64Code(nodes)))
                    {
                        _classToGen.Enqueue(subDataNode);
                    }
                }
            }
            else if (fieldNode.Type == "map")
            {
                var pair = fieldNode.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                if (!IsPrimitive(keyType.Type) && !_hashToClassNameMap.ContainsKey(keyType.GetHash64Code(nodes)))
                {
                    _classToGen.Enqueue(keyType);
                }
                
                if (!IsPrimitive(valueType.Type) && !_hashToClassNameMap.ContainsKey(valueType.GetHash64Code(nodes)))
                {
                    _classToGen.Enqueue(valueType);
                }
                // ugly patch
                else if (valueType.Type == "vector")
                {
                    var subDataNode = valueType.Children(nodes)[0].Children(nodes)[1];
                    if (!IsPrimitive(subDataNode.Type) && !_hashToClassNameMap.ContainsKey(subDataNode.GetHash64Code(nodes)))
                    {
                        _classToGen.Enqueue(subDataNode);
                    }
                }
            }
        }
        
        sb.AppendLine($"}}");
    }

    private void GenerateConstructor(string className, List<TypeTreeNode> children, List<TypeTreeNode> nodes)
    {
        sb.AppendLine();
        sb.AppendLine($"    public {className}(IReader reader)");
        sb.AppendLine($"    {{");
        foreach (var fieldNode in children)
        {
            sb.Append($"        {SanitizeName(fieldNode.Name)} = ");
            if (IsPrimitive(fieldNode.Type))
            {
                sb.Append(GetFieldConstructor(fieldNode, nodes));
            }
            else
            {
                var filedHash64 = fieldNode.GetHash64Code(nodes);
                if (_hashToClassNameMap.TryGetValue(filedHash64, out var filedName))
                {
                    sb.Append($"new {filedName}(reader)");
                }
                else
                {
                    sb.Append($"new {SanitizeName($"{fieldNode.Type}_{filedHash64}")}(reader)");
                }
            }
            sb.AppendLine(";");
            //sb.AppendLine($"Console.WriteLine($\"{fieldNode.Type} {fieldNode.Name}: pos: {{reader.Position}}\");");
            
            var align = fieldNode.RequiresAlign(nodes);
            if (align)
                sb.AppendLine($"        reader.Align(4);");
        }
        sb.AppendLine($"    }}");
    }

    private void GenerateToPlainTextMethod(TypeTreeNode current, List<TypeTreeNode> children, List<TypeTreeNode> nodes)
    {
        sb.AppendLine();
        sb.AppendLine($"    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = \"\")");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        sb ??= new StringBuilder();");
        sb.AppendLine($"        var childIndent = indent + \"    \";");

        if (current == nodes[0])
        {
            sb.AppendLine($"        sb.AppendLine(\"{current.Type} {current.Name}\");");
        }

        foreach (var fieldNode in children)
        {
            sb.Append(GetToPlainTextMethod(fieldNode, nodes, "        ", SanitizeName(fieldNode.Name)));
        }
        sb.AppendLine($"        return sb;");
        sb.AppendLine($"    }}");
    }

    private static string GetToPlainTextMethod(TypeTreeNode current, List<TypeTreeNode> nodes, string indent, string valueName)
    {
        var subSb = new StringBuilder();
        if (IsPrimitive(current.Type))
        {
            if (current.Type == "vector")
            {
                var dataNode = current.Children(nodes)[0].Children(nodes)[1];
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name}\");");
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    Array Array\");");
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    int size = {{{valueName}.Count}}\");");
                subSb.AppendLine($"{indent}for (int {current.Name}Count = 0; {current.Name}Count < {valueName}.Count; {current.Name}Count++)");
                subSb.AppendLine($"{indent}{{");
                subSb.AppendLine($"{indent}    sb.AppendLine($\"{{childIndent}}        [{{{current.Name}Count}}]\");");
                subSb.AppendLine($"{indent}    var {current.Name}childIndentBackUp = childIndent;");
                subSb.AppendLine($"{indent}    childIndent = $\"{{childIndent}}        \";");
                subSb.Append(GetToPlainTextMethod(dataNode, nodes, indent + "    ", $"{valueName}[{current.Name}Count]"));
                subSb.AppendLine($"{indent}    childIndent = {current.Name}childIndentBackUp;");
                subSb.AppendLine($"{indent}}}");
            }
            else if (current.Type == "map")
            {
                var pair = current.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name}\");");
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    Array Array\");");
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}    int size = {{{valueName}.Count}}\");");
                subSb.AppendLine($"{indent}for (int {current.Name}Count = 0; {current.Name}Count < {current.Name}.Count; {current.Name}Count++)");
                subSb.AppendLine($"{indent}{{");
                subSb.AppendLine($"{indent}    sb.AppendLine($\"{{childIndent}}        [{{{current.Name}Count}}]\");");
                subSb.AppendLine($"{indent}    sb.AppendLine($\"{{childIndent}}        pair data\");");
                subSb.Append(GetToPlainTextMethod(keyType, nodes, indent + "        ", $"{valueName}[{current.Name}Count].Key"));
                subSb.Append(GetToPlainTextMethod(valueType, nodes, indent + "        ", $"{valueName}[{current.Name}Count].Value"));
                subSb.AppendLine($"{indent}}}");
            }
            else if (current.Type == "TypelessData")
            {
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name}\");");
                subSb.AppendLine($"{indent}{valueName}.ToPlainText(sb, childIndent);");
            }
            else if (current.Type == "string")
            {
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name} = \\\"{{{valueName}}}\\\"\");");
            }
            else
            {
                subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name} = {{{valueName}}}\");");
            }
        }
        else
        {
            subSb.AppendLine($"{indent}sb.AppendLine($\"{{childIndent}}{current.Type} {current.Name}\");");
            subSb.AppendLine($"{indent}{valueName}.ToPlainText(sb, childIndent);");
        }

        return subSb.ToString();
    }

    private static string GetFieldConstructor(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (!IsPrimitive(current.Type))
        {
            return $"new {SanitizeName(current.Type)}(reader)";
        }
        
        switch(current.Type)
        {
            case "SInt8" :
                return "reader.ReadInt8()";
            case "UInt8" : 
                return "reader.ReadUInt8()";
            case "char" :
                return "BitConverter.ToChar(reader.ReadBytes(2), 0)";
            case "short" :
            case "SInt16" :
                return "reader.ReadInt16()";
            case "UInt16" : 
            case "unsigned short" :
                return "reader.ReadUInt16()";
            case "int" : 
            case "SInt32" :
                return "reader.ReadInt32()";
            case "UInt32" : 
            case "unsigned int" : 
            case "Type*" :
                return "reader.ReadUInt32()";
            case "long long" : 
            case "SInt64" :
                return "reader.ReadInt64()";
            case "UInt64" : 
            case "unsigned long long" : 
            case "FileSize" :
                return "reader.ReadUInt64()";
            case "float" :
                return "reader.ReadFloat()";
            case "double" :
                return "reader.ReadDouble()";
            case "bool" :
                return "reader.ReadBoolean()";
            case "string" :
                return "reader.ReadSizedString()";
            case "TypelessData" :
                return "new TypelessData(reader);";
            case "vector":
            {
                var dataNode = current.Children(nodes)[0].Children(nodes)[1];
                var hash64 = dataNode.GetHash64Code(nodes);
                string constructor = 
                    IsPrimitive(dataNode.Type) 
                        ? $"r => {GetFieldConstructor(dataNode, nodes).Replace("reader", "r")}"
                        : $"r => new {SanitizeName($"{dataNode.Type}_{hash64}")}(r)";
                
                return $"reader.ReadListWithAlign(reader.ReadInt32(), {constructor}, {dataNode.RequiresAlign(nodes).ToString().ToLower()})";
            }
            case "map":
            {
                var pair = current.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                var keyHash64 = keyType.GetHash64Code(nodes);
                var valueHash64 = valueType.GetHash64Code(nodes);
                string keyConstructor = 
                    IsPrimitive(keyType.Type) 
                        ? $"r => {GetFieldConstructor(keyType, nodes).Replace("reader", "r")}"
                        : $"r => new {SanitizeName($"{keyType.Type}_{keyHash64}")}(r)";
                string valueConstructor = 
                    IsPrimitive(valueType.Type) 
                        ? $"r => {GetFieldConstructor(valueType, nodes).Replace("reader", "r")}"
                        : $"r => new {SanitizeName($"{valueType.Type}_{valueHash64}")}(r)";
                return $"reader.ReadMapWithAlign(reader.ReadInt32(), {keyConstructor}, {valueConstructor}, {keyType.RequiresAlign(nodes).ToString().ToLower()}, {valueType.RequiresAlign(nodes).ToString().ToLower()})";
            }
            default: throw new NotSupportedException($"Unity type not supported: {current.Type}");
        }
    }

    private static string GetCSharpType(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (!IsPrimitive(current.Type))
        {
            return SanitizeName(current.Type);
        }

        switch(current.Type)
        {
            case "SInt8" :
                return "sbyte";
            case "UInt8" : 
                return "byte";
            case "char" :
                return "char";
            case "short" :
            case "SInt16" :
                return "Int16";
            case "UInt16" : 
            case "unsigned short" :
                return "UInt16";
            case "int" : 
            case "SInt32" :
                return "Int32";
            case "UInt32" : 
            case "unsigned int" : 
            case "Type*" :
                return "UInt32";
            case "long long" : 
            case "SInt64" :
                return "Int64";
            case "UInt64" : 
            case "unsigned long long" : 
            case "FileSize" :
                return "UInt64";
            case "float" :
                return "float";
            case "double" :
                return "double";
            case "bool" :
                return "bool";
            case "string" :
                return "string";
            case "TypelessData" :
                return "TypelessData";
            case "vector":
            {
                var dataNode = current.Children(nodes)[0].Children(nodes)[1];
                var hash64 = dataNode.GetHash64Code(nodes);
                return 
                    IsPrimitive(dataNode.Type) 
                        ? $"List<{GetCSharpType(dataNode, nodes)}>" 
                        : $"List<{SanitizeName($"{dataNode.Type}_{hash64}")}>" ;
            }
            case "map":
            {
                var pair = current.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                var keyHash64 = keyType.GetHash64Code(nodes);
                var valueHash64 = valueType.GetHash64Code(nodes);
                string keyName = 
                    IsPrimitive(keyType.Type) 
                        ? SanitizeName(keyType.Type)
                        : SanitizeName($"{keyType.Type}_{keyHash64}");
                string valueName = 
                    IsPrimitive(valueType.Type) 
                        ? SanitizeName(valueType.Type)
                        : SanitizeName($"{valueType.Type}_{valueHash64}");
                return $"List<KeyValuePair<{keyName}, {valueName}>>";
            }
            default: throw new NotSupportedException($"Unity type not supported: {current.Type}");
        }
    }

    private static bool IsPrimitive(string unityType)
    {
        return unityType switch
        { 
            "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" or "vector" or "map" or "TypelessData" => true,
            _ => false
        };
    }

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new Exception("Unexpected null or empty name for type generate.");
        
        var sanitized = new StringBuilder(name.Length);

        if (char.IsDigit(name[0]))
        {
            sanitized.Append('_');
        }

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }
        return sanitized.ToString();
    }
}