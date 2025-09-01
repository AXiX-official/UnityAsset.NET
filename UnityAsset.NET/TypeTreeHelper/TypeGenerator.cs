using System.Text;
using Microsoft.CodeAnalysis.CSharp;
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

        if (nodes.Count == 0)
            return string.Empty;

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
        string interfaceName = GetInterfaceName(current, nodes);
        sb.AppendLine($"public class {className} : {interfaceName}");
        sb.AppendLine($"{{");

        sb.AppendLine($"    public string ClassName => \"{current.Type}\";");

        foreach (var fieldNode in children)
        {
            sb.AppendLine($"    [OriginalName(\"{fieldNode.Name}\")]");
            if (IsPrimitive(fieldNode.Type) || PreDefinedHelper.IsPreDefinedType(fieldNode.Type))
            {
                sb.AppendLine($"    public {GetCSharpType(fieldNode, nodes)} {SanitizeName(fieldNode.Name)} {{ get; }}");
            }
            else if (IsVector(fieldNode, nodes))
            {
                sb.AppendLine($"    public {GetCSharpType(fieldNode, nodes)} {SanitizeName(fieldNode.Name)} {{ get; }}");
                var dataNode = fieldNode.Children(nodes)[0].Children(nodes)[1];
                if (!IsPrimitive(dataNode.Type) && !_hashToClassNameMap.ContainsKey(dataNode.GetHash64Code(nodes)) && !PreDefinedHelper.IsPreDefinedType(dataNode.Type))
                {
                    _classToGen.Enqueue(dataNode);
                }
            }
            else if (IsMap(fieldNode, nodes))
            {
                sb.AppendLine($"    public {GetCSharpType(fieldNode, nodes)} {SanitizeName(fieldNode.Name)} {{ get; }}");
                var pair = fieldNode.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                if (!IsPrimitive(keyType.Type) && !_hashToClassNameMap.ContainsKey(keyType.GetHash64Code(nodes)) && !PreDefinedHelper.IsPreDefinedType(keyType.Type))
                {
                    _classToGen.Enqueue(keyType);
                }
                if (!IsPrimitive(valueType.Type) && !_hashToClassNameMap.ContainsKey(valueType.GetHash64Code(nodes)) && !PreDefinedHelper.IsPreDefinedType(valueType.Type))
                {
                    _classToGen.Enqueue(valueType);
                }
            }
            else
            {
                var filedHash64 = fieldNode.GetHash64Code(nodes);
                var filedInterfaceName = $"I{fieldNode.Type}";
                if (_hashToClassNameMap.TryGetValue(filedHash64, out var filedName))
                {
                    if (PreDefinedHelper.IsPreDefinedInterface(filedInterfaceName))
                    {
                        sb.AppendLine($"    public {filedInterfaceName} {SanitizeName(fieldNode.Name)} {{ get; }}");
                    }
                    else
                    {
                        sb.AppendLine($"    public {filedName} {SanitizeName(fieldNode.Name)} {{ get; }}");
                    }
                }
                else
                {
                    if (PreDefinedHelper.IsPreDefinedInterface(filedInterfaceName))
                    {
                        sb.AppendLine($"    public {filedInterfaceName} {SanitizeName(fieldNode.Name)} {{ get; }}");
                    }
                    else
                    {
                        sb.AppendLine($"    public {SanitizeName($"{fieldNode.Type}_{filedHash64}")} {SanitizeName(fieldNode.Name)} {{ get; }}");
                    }
                    _classToGen.Enqueue(fieldNode);
                }
            }
        }

        GenerateConstructor(className, children, nodes);
        GenerateToPlainTextMethod(current, children, nodes);
        
        sb.AppendLine($"}}");
    }

    private string GetInterfaceName(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Level == 0)
        {
            switch (current.Type)
            {
                case "Mesh" :
                    return "IMesh";
                case "TextAsset" :
                    return "ITextAsset";
                case "Texture2D" :
                    return "ITexture2D";
                default:
                {
                    bool hasNameField = current.Children(nodes).Any(node => node.Name == "m_Name");
                    return hasNameField ? "INamedAsset" : "IAsset";
                }
            }
        }

        var interfaceName = $"I{current.Type}";
        if (PreDefinedHelper.IsPreDefinedInterface(interfaceName))
        {
            return interfaceName;
        }

        return "IUnityType";
    }
    

    private void GenerateConstructor(string className, List<TypeTreeNode> children, List<TypeTreeNode> nodes)
    {
        sb.AppendLine();
        sb.AppendLine($"    public {className}(IReader reader)");
        sb.AppendLine($"    {{");
        foreach (var fieldNode in children)
        {
            sb.Append($"        {SanitizeName(fieldNode.Name)} = ");
            if (IsPrimitive(fieldNode.Type) || IsVector(fieldNode, nodes) || IsMap(fieldNode, nodes) || PreDefinedHelper.IsPreDefinedType(fieldNode.Type))
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
            if (current.Type == "TypelessData" || current.Type == "StreamingInfo")
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
        else if (IsVector(current, nodes))
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
        else if (IsMap(current, nodes))
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
            if (PreDefinedHelper.IsPreDefinedType(current.Type))
            {
                return $"new {current.Type}(reader)";
            }
            
            if (IsVector(current, nodes))
            {
                var dataNode = current.Children(nodes)[0].Children(nodes)[1];
                string constructor = $"r => {GetFieldConstructor(dataNode, nodes).Replace("reader", "r")}";
                
                var dataNodeInterfaceName = $"I{dataNode.Type}";
                var genericTypeArgument = PreDefinedHelper.IsPreDefinedInterface(dataNodeInterfaceName) ? $"<{dataNodeInterfaceName}>" : $"";
                return $"reader.ReadListWithAlign{genericTypeArgument}(reader.ReadInt32(), {constructor}, {dataNode.RequiresAlign(nodes).ToString().ToLower()})";
            }

            if (IsMap(current, nodes))
            {
                var pair = current.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                string keyConstructor = $"r => {GetFieldConstructor(keyType, nodes).Replace("reader", "r")}";
                string valueConstructor = $"r => {GetFieldConstructor(valueType, nodes).Replace("reader", "r")}";
                return $"reader.ReadMapWithAlign(reader.ReadInt32(), {keyConstructor}, {valueConstructor}, {keyType.RequiresAlign(nodes).ToString().ToLower()}, {valueType.RequiresAlign(nodes).ToString().ToLower()})";
            }
            
            var hash64 = current.GetHash64Code(nodes);
            var typeName = $"{current.Type}_{hash64}";
            return $"new {SanitizeName(typeName)}(reader)";
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
            default: throw new NotSupportedException($"Unity type not supported: {current.Type}");
        }
    }

    private static string GetCSharpType(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (!IsPrimitive(current.Type))
        {
            if (PreDefinedHelper.IsPreDefinedType(current.Type))
            {
                return current.Type;
            }
            
            var interfaceName = $"I{current.Type}";
            if (PreDefinedHelper.IsPreDefinedInterface(interfaceName))
            {
                return interfaceName;
            }
            
            if (IsVector(current, nodes))
            {
                var dataNode = current.Children(nodes)[0].Children(nodes)[1];
                return $"List<{GetCSharpType(dataNode, nodes)}>";
            }

            if (IsMap(current, nodes))
            {
                var pair = current.Children(nodes)[0].Children(nodes)[1].Children(nodes);
                var keyType = pair[0];
                var valueType = pair[1];
                return $"List<KeyValuePair<{GetCSharpType(keyType, nodes)}, {GetCSharpType(valueType, nodes)}>>";
            }

            var hash64 = current.GetHash64Code(nodes);
            return SanitizeName($"{current.Type}_{hash64}");
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
            default: throw new NotSupportedException($"Unity type not supported: {current.Type}");
        }
    }

    private static bool IsPrimitive(string unityType)
    {
        return unityType switch
        { 
            "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" => true,
            _ => false,
        };
    }

    private static bool IsVector(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Type == "vector") return true;
        var children = current.Children(nodes);
        if (children.Count == 0) return false;
        if (children[0].Type == "Array") return true;
        return false;
    }
    
    private static bool IsMap(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Type == "map") return true;
        return false;
    }
    
    public static bool IsKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None;
    }

    public static bool IsReservedKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None && 
               SyntaxFacts.IsReservedKeyword(kind);
    }

    public static bool IsContextualKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None && 
               SyntaxFacts.IsContextualKeyword(kind);
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
        var fixedName = sanitized.ToString();
        if (IsReservedKeyword(fixedName) || IsContextualKeyword(fixedName))
        {
            return "@" + fixedName;
        }
        return fixedName;
    }
}