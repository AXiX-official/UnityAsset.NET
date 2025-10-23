using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn;

public class SemanticModelBuilder
{
    private readonly Dictionary<int, IUnityTypeInfo> _cache = new();
    public Dictionary<int, ClassTypeInfo> DiscoveredTypes { get; } = new();
    
    public ClassTypeInfo? Build(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (Helper.IsPreDefinedType(current))
            return null;
        
        var inferredInterfaceName = $"I{current.Type}";
        var type = ((IReadOnlyDictionary<string, Type?>)Helper.PreDefinedInterfaceMap).GetValueOrDefault(inferredInterfaceName, null);
        
        var children = current.Children(nodes);
        var fields = new List<UnityFieldInfo>();

        var interfaceProperties = type != null ? RoslynBuilderHelper.GetAllInterfaceProperties(type) : null;

        interfaceProperties?.Remove("ClassName");
        
        foreach (var node in children)
        {
            
            var sanitizedName = IdentifierSanitizer.SanitizeName(node.Name);
            bool isNullable = interfaceProperties?.TryGetValue(sanitizedName, out var field) == true 
                              && RoslynBuilderHelper.IsNullable(field.PropertyType);
            
            var fieldTypeInfo = ResolveNode(node, nodes);

            var fieldName = RoslynBuilderHelper.SanitizeName(node.Name); 
            
            fields.Add(new UnityFieldInfo
            {
                Name = fieldName,
                RequireAlign = node.RequiresAlign(nodes),
                IsNullable = isNullable,
                DeclaredTypeSyntax = interfaceProperties?.TryGetValue(fieldName, out var interfaceProperty) ?? false
                    ? RoslynBuilderHelper.GetTypeSyntax(interfaceProperty.PropertyType) : fieldTypeInfo.ToTypeSyntax(),
                TypeInfo = fieldTypeInfo
            });
        }
        
        
        var interfaceName = type?.Name ?? (Helper.IsNamedAsset(current, nodes) ? "INamedAsset" : current == nodes[0] ? "IAsset" : "IUnityType");
        var generatedClassName = IdentifierSanitizer.SanitizeName($"{current.Type}_{current.GetHashCode(nodes)}");

        var classTypeInfo = new ClassTypeInfo
        {
            Name = current.Type,
            GeneratedClassName = generatedClassName,
            InterfaceName = interfaceName,
            Fields = fields
        };
        
        DiscoveredTypes[current.GetHashCode(nodes)] = classTypeInfo;
        return classTypeInfo;
    }

    private IUnityTypeInfo ResolveNode(TypeTreeNode node, List<TypeTreeNode> allNodes)
    {
        var hash = node.GetHashCode(allNodes);
        if (_cache.TryGetValue(hash, out var cachedInfo)) return cachedInfo;

        IUnityTypeInfo typeInfo;
        if (RoslynBuilderHelper.IsPrimitive(node))
        {
            var csharpType = RoslynBuilderHelper.GetCSharpPrimitiveType(node.Type);
            typeInfo = new PrimitiveTypeInfo
            {
                OriginalTypeName = node.Type,
                PrimitiveSyntax = SyntaxFactory.ParseTypeName(csharpType)
            };
        }
        else if (RoslynBuilderHelper.IsGenericPPtr(node))
        {
            var genericTypeName = node.Type.Substring(5, node.Type.Length - 6);
            if (genericTypeName == "Object") genericTypeName = "TypeTreeHelper.PreDefined.Types.Object";
            else genericTypeName = Helper.GetPredefinedTypeOrInterfaceName(genericTypeName); // temp solve
            typeInfo = new GenericPPtrTypeInfo
            {
                GenericTypeSyntax = SyntaxFactory.ParseTypeName(genericTypeName)
            };
        }
        else if (RoslynBuilderHelper.IsPreDefinedType(node))
        {
            typeInfo = new PredefinedTypeInfo
            {
                PredefinedTypeSyntax = SyntaxFactory.ParseTypeName(RoslynBuilderHelper.SanitizeName(node.Type))
            };
        }
        else if (RoslynBuilderHelper.IsPair(node))
        {
            var children = node.Children(allNodes);
            var item1Node = children[0];
            var item2Node = children[1];
            typeInfo = new PairTypeInfo
            {
                Item1Type = ResolveNode(item1Node, allNodes),
                Item2Type = ResolveNode(item2Node, allNodes),
                Item1RequireAlign = item1Node.RequiresAlign(allNodes),
                Item2RequireAlign = item2Node.RequiresAlign(allNodes)
            };
        }
        else if (RoslynBuilderHelper.IsVector(node, allNodes))
        {
            var elementNode = node.Children(allNodes)[0].Children(allNodes)[1];
            typeInfo = new VectorTypeInfo
            {
                ElementType = ResolveNode(elementNode, allNodes),
                ElementRequireAlign = elementNode.RequiresAlign(allNodes)
            };
        }
        else if (RoslynBuilderHelper.IsMap(node))
        {
            var pairNode = node.Children(allNodes)[0].Children(allNodes)[1];
            typeInfo = new MapTypeInfo
            {
                PairType = (PairTypeInfo)ResolveNode(pairNode, allNodes),
                PairRequireAlign = pairNode.RequiresAlign(allNodes)
            };
        }
        else // Complex Type
        {
            var classTypeInfo = Build(node, allNodes);

            DiscoveredTypes[hash] = classTypeInfo!;
            typeInfo = classTypeInfo!;
        }

        _cache[hash] = typeInfo;
        return typeInfo;
    }
}