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
        
        var includedRootTypes = rootTypeNodesMap.Where(kvp =>
            !Helper.ExcludedTypes.Contains(kvp.Key) && (Helper.IncludedTypes.Contains(kvp.Key) || Helper.IncludedPPTrGenricTypes.Contains(kvp.Key))
        ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        var excludedRootTypes = rootTypeNodesMap.Where(kvp =>
            !Helper.IncludedTypes.Contains(kvp.Key)
        ).Select(kvp => kvp.Value);
        
        DiscoverAllSubNodes(includedRootTypes.Values, out var subNodes);
        DiscoverLeftSubNodes(excludedRootTypes, subNodes);
        
        _subNodes = subNodes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList()
        );
        
        foreach (var (className, rootNodes) in includedRootTypes)
        {
            var compilationUnit = GenerateClassInterface(className, rootNodes, true);
                    
            var formattedSource = compilationUnit.NormalizeWhitespace(elasticTrivia: true).ToFullString();
                    
            var filePath = Path.Combine(rootClassDir, $"I{className}.g.cs");
            File.WriteAllText(filePath, formattedSource);
        }

        foreach (var (className, subNodeList) in _subNodes)
        {
            var subCompilationUnit = GenerateClassInterface(className, subNodeList, false);

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
        
            if (type.StartsWith("PPtr<"))
                type = "PPtr";
            
            if (Helper.IsPrimitive(type))
                continue;
            
            if (!Helper.ExcludedBasicTypes.Contains(type) && !Helper.NoInterfaceTypes.Contains(type) && !Helper.ExcludedTypes.Contains(type))
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
    
    private void DiscoverLeftSubNodesRecursive(TpkUnityTreeNode node, Dictionary<string, HashSet<TpkUnityTreeNode>> subNodes, bool forceAdd = false)
    {
        foreach (var subNode in node.SubNodes)
        {
            var type = subNode.TypeName;
        
            if (type.StartsWith("PPtr<"))
                type = "PPtr";
            
            if (Helper.IsPrimitive(type))
                continue;
            
            if (subNodes.TryGetValue(type, out var set))
            {
                set.Add(subNode);
                forceAdd = true;
            }
            else if (forceAdd)
            {
                if (!Helper.ExcludedBasicTypes.Contains(type) && !Helper.NoInterfaceTypes.Contains(type) && !Helper.ExcludedTypes.Contains(type))
                {
                    subNodes[type] = set = new HashSet<TpkUnityTreeNode>();
                    set.Add(subNode);
                }
            }
            
            DiscoverLeftSubNodesRecursive(subNode, subNodes, forceAdd);
        }
    }
    
    private void DiscoverLeftSubNodes(IEnumerable<IEnumerable<TpkUnityTreeNode>> allNodes, Dictionary<string, HashSet<TpkUnityTreeNode>> subNodes)
    {
        foreach (var nodes in allNodes)
        foreach (var rootNode in nodes)
        {
            DiscoverLeftSubNodesRecursive(rootNode, subNodes);
        }
    }

    private void DiscoverAllSubNodes(IEnumerable<IEnumerable<TpkUnityTreeNode>> allNodes, out Dictionary<string, HashSet<TpkUnityTreeNode>> subNodes)
    {
        subNodes = new();
    
        foreach (var nodes in allNodes)
        foreach (var rootNode in nodes)
        {
            DiscoverSubNodesRecursive(rootNode, subNodes);
        }
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
    }
    
    private CompilationUnitSyntax GenerateClassInterface(string className, List<TpkUnityTreeNode> rootNodes, bool isRootClass)
    {
        var usingDirectives = SyntaxFactory.List([
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("OneOf")),
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

        var properties = new Dictionary<string , List<string>>();

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

                if (properties.TryGetValue(name, out var prop))
                {
                    prop.Add(interfaceName);
                }
                else
                {
                    properties[name] = [interfaceName];
                }
            }
        }

        foreach (var (propertyName, proptypes) in properties)
        {
            bool isNullable = proptypes.Count != rootNodes.Count;
            var baseTypeName = GetOptionalType(proptypes);
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

    private static string GetOptionalType(List<string> types)
    {
        var uniqueTypes = types.Distinct().ToList();
        
        if (uniqueTypes.Count == 1)
            return types.First();
        
        string MultiTypesToOne(List<string> typeList)
        {
            if (typeList.All(t => t.StartsWith("List")))
                return $"List<{MultiTypesToOne(typeList.Select(t => t.Substring(5, t.Length - 6)).ToList())}>";
            if (typeList.All(t => !Helper.IsCSharpPrimitive(t)))
                return "IUnityType";
            return "object";
        }

        if (uniqueTypes.Count > 8)
        {
            // OneOf supports up to 8 types
            return MultiTypesToOne(uniqueTypes);
        }
        
        return $"OneOf<{string.Join(", ", uniqueTypes)}>";
    }

    private string GetInterfaceName(TpkUnityTreeNode node, out string genericTypeName)
    {
        genericTypeName = string.Empty;
        
        if (Helper.IsPrimitive(node.TypeName))
            return Helper.GetCSharpPrimitiveType(node.TypeName);

        if (node.TypeName.StartsWith("PPtr<"))
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

        if (node.TypeName == "vector" || node.TypeName == "staticvector" || node.TypeName == "map")
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