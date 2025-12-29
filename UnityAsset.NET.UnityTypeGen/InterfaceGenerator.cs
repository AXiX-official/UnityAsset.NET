using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.TypeTreeHelper.Compiler;

namespace UnityAsset.NET.UnityTypeGen;

public class InterfaceGenerator
{
    private Dictionary<string, List<TpkUnityTreeNode>> _subNodes = new();

    public static string RootClassFolderName { get; set; } = "Classes";
    public static string SubClassFolderName { get; set; } = "Interfaces";
    
    private Dictionary<int, string> _cachedGenericTypes = new();
    
    public void GenerateInterfaces(string outputDirectory, Dictionary<string,List<TpkUnityTreeNode>> rootTypeNodesMap)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
        else //clean up
        {
            DirectoryInfo di = new DirectoryInfo(outputDirectory);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }
        
        var rootClassDir = Path.Combine(outputDirectory, RootClassFolderName);
        if (!Directory.Exists(rootClassDir))
            Directory.CreateDirectory(rootClassDir);
        var subClassDir = Path.Combine(outputDirectory, SubClassFolderName);
        if (!Directory.Exists(subClassDir))
            Directory.CreateDirectory(subClassDir);
        
        DiscoverAllSubNodes(rootTypeNodesMap.Values);
        
        foreach (var (className, rootNodes) in rootTypeNodesMap)
        {
            if (Helper.ExcludedTypes.Contains(className)) continue;
            
            var compilationUnit = GenerateClassInterface(className, rootNodes, true);
                    
            var formattedSource = compilationUnit.NormalizeWhitespace(elasticTrivia: true).ToFullString();
                    
            var filePath = Path.Combine(rootClassDir, $"I{className}.g.cs");
            File.WriteAllText(filePath, formattedSource);
        }

        foreach (var (className, subNodes) in _subNodes)
        {
            var subCompilationUnit = GenerateClassInterface(className, subNodes, false);

            var subFormattedSource = subCompilationUnit.NormalizeWhitespace(elasticTrivia: true).ToFullString();
            var subFilePath = Path.Combine(subClassDir, $"I{className}.g.cs");
            File.WriteAllText(subFilePath, subFormattedSource);
        }
    }
    
    private void DiscoverSubNodesRecursive(TpkUnityTreeNode node, in Dictionary<string, HashSet<TpkUnityTreeNode>> subNodes)
    {
        foreach (var subNode in node.SubNodes)
        {
            var type = subNode.TypeName;
        
            if (type.StartsWith("PPtr<") && type.EndsWith(">"))
                type = "PPtr";

            if (!Helper.IsPrimitive(type) && !Helper.ExcludedTypes.Contains(type) && !Helper.NoInterfaceTypes.Contains(type))
            {
                if (!subNodes.TryGetValue(type, out var set))
                {
                    subNodes[type] = set = new HashSet<TpkUnityTreeNode>();
                }
                set.Add(subNode);
            }

            DiscoverSubNodesRecursive(subNode, subNodes);
        }
    }

    private void DiscoverAllSubNodes(IEnumerable<IEnumerable<TpkUnityTreeNode>> allNodes)
    {
        var subNodes = new Dictionary<string, HashSet<TpkUnityTreeNode>>();
    
        foreach (var nodes in allNodes)
        foreach (var rootNode in nodes)
        {
            DiscoverSubNodesRecursive(rootNode, subNodes);
        }
    
        _subNodes = subNodes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList()
        );
    }

    // workaround for Keyframe and other generic types
    private string GetGenricType(TpkUnityTreeNode node)
    {
        if (_cachedGenericTypes.TryGetValue(node.Index, out var type))
        {
            return type;
        }

        if (node.TypeName == "Keyframe")
        {
            type = node.SubNodes[1].TypeName;
            _cachedGenericTypes[node.Index] = type;
            return type;
        }
        
        if (node.TypeName == "AnimationCurve")
        {
            type = GetGenricType(node.SubNodes[0].SubNodes[0].SubNodes[1]); // keyframe<T> m_Curve
            _cachedGenericTypes[node.Index] = type;
            return type;
        }
        
        // assert we can cut generic type spreading here
        type = string.Empty;
        _cachedGenericTypes[node.Index] = type;
        return type;
        /*var propertyNodes = node.SubNodes.Where(n => n.TypeName == "AnimationCurve").ToList();
        if (propertyNodes.Count == 0)
        {
            type = string.Empty;
            _cachedGenericTypes[node.Index] = type;
            return type;
        }

        var uniqueTypes = new HashSet<string>(propertyNodes.Select(GetGenricType).Where(t => t != string.Empty));
        
        if (uniqueTypes.Count == 0)
        {
            type = string.Empty;
            _cachedGenericTypes[node.Index] = type;
            return type;
        }
            
        if (uniqueTypes.Count != 1)
            throw new Exception($"Type {node.TypeName} has more than one generic type");
        
        type = uniqueTypes.First();
        _cachedGenericTypes[node.Index] = type;
        return type;*/
    }
    
    private CompilationUnitSyntax GenerateClassInterface(string className, List<TpkUnityTreeNode> rootNodes, bool isRootClass)
    {
        var usingDirectives = SyntaxFactory.List([
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.TypeTree.PreDefined.Types"))
        ]);
        
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
            SyntaxFactory.ParseName("UnityAsset.NET.TypeTree.PreDefined.Interfaces")
            );
        
        bool isNamedAsset = rootNodes.All(rootNode =>
            rootNode.SubNodes.Any(sb => sb.Name == "m_Name")
        );
        
        // TODO: more specific base interfaces
        var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration($"I{className}")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            //.AddTypeParameterListParameters(SyntaxFactory.TypeParameter("T"))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(
                isRootClass ? (isNamedAsset ? "INamedAsset" : "IUnityAsset") : "IPreDefinedInterface")));
        
        var members = new List<MemberDeclarationSyntax>();

        var properties = new Dictionary<string , (string PropInterface, int Count)>();

        foreach (var rootNode in rootNodes)
        {
            foreach (var subNode in rootNode.SubNodes)
            {
                
                var name = subNode.Name;
                if (isNamedAsset && name == "m_Name")
                    continue;
                var interfaceName = Helper.IsPrimitive(subNode.TypeName) 
                    ? Helper.GetCSharpPrimitiveType(subNode.TypeName) 
                    : GetInterfaceName(subNode, out var genericTypeName);

                if (!properties.ContainsKey(name))
                {
                    properties[name] = (interfaceName, 1);
                }
                else
                {
                    var property = properties[name];
                    property.Count += 1;
                    property.PropInterface = GetOptionalType(property.PropInterface, interfaceName);
                    properties[name] = property;
                }
            }
        }

        foreach (var (propertyName, (baseTypeName, count)) in properties)
        {
            bool isNullable = count != rootNodes.Count;
            var typeName = isNullable ? $"{baseTypeName}?" : baseTypeName;
            var declaredType = SyntaxFactory.ParseTypeName(typeName);
            
            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(declaredType, Helper.SanitizeName(propertyName))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            
            if (isNullable)
            {
                var getterWithBody = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));
                propertyDeclaration = propertyDeclaration.AddAccessorListAccessors(getterWithBody);
            }
            else
            {
                var abstractGetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                propertyDeclaration = propertyDeclaration.AddAccessorListAccessors(abstractGetter);
            }
            
            members.Add(propertyDeclaration);
        }
        
        interfaceDeclaration = interfaceDeclaration.AddMembers(members.ToArray());
        
        var leadingTrivia = SyntaxFactory.TriviaList(
            SyntaxFactory.Comment("// <auto-generated>"),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.Comment("// Warning: This file is auto-generated. Do not edit manually."),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.Comment("// </auto-generated>"),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed,
    
            SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true)),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed
        );
        
        return SyntaxFactory.CompilationUnit()
            .WithUsings(usingDirectives)
            .AddMembers(namespaceDeclaration.AddMembers(interfaceDeclaration))
            .WithLeadingTrivia(leadingTrivia);
    }

    private static string GetOptionalType(string firstType, string secondType)
    {
        if (firstType == secondType)
            return firstType;
        
        if (!Helper.IsCSharpPrimitive(firstType) || !Helper.IsCSharpPrimitive(secondType))
            return "IUnityType";
        
        if (firstType == "string" || secondType == "string")
            return "IUnityType";
        
        var rank = Math.Max(Helper.PrimitiveNumericRankings[firstType], Helper.PrimitiveNumericRankings[secondType]);
        return Helper.PrimitiveNumericMap[rank];
    }

    private string GetInterfaceName(TpkUnityTreeNode node, out string genericTypeName)
    {
        genericTypeName = string.Empty;
        
        if (Helper.IsPrimitive(node.TypeName))
            return Helper.GetCSharpPrimitiveType(node.TypeName);

        if (node.TypeName.StartsWith("PPtr"))
        {
            var genericType = node.TypeName.Substring(5, node.TypeName.Length - 6);
            return genericType == "Object"
                ? "PPtr<UnityObject>"
                : Helper.IncludedPPTrGenricTypes.Contains(genericType)
                    ? $"PPtr<I{genericType}>"
                    : "PPtr<IUnityType>";
        }
        
        if (node.TypeName == "pair")
            return $"ValueTuple<{GetInterfaceName(node.SubNodes[0], out _)}, {GetInterfaceName(node.SubNodes[1], out _)}>";

        if (node.TypeName == "vector" || node.TypeName == "map")
            return GetInterfaceName(node.SubNodes[0], out _);
        
        if (node.TypeName == "Array")
            return $"List<{GetInterfaceName(node.SubNodes[1], out _)}>";
        
        if (Helper.PreDefinedTypes.Contains(node.TypeName))
            return node.TypeName;
        
        genericTypeName = GetGenricType(node);

        return genericTypeName == string.Empty
            ? $"I{node.TypeName}"
            : $"I{node.TypeName}<{genericTypeName}>";
        
        //return $"I{node.TypeName}";
    }
}