using System.Reflection;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Builder;

public class TypeSyntaxBuilder
{
    private readonly Dictionary<int, TypeDeclarationNode> _cache = new();
    public Dictionary<int, ClassSyntaxNode> DiscoveredTypes { get; } = new();

    public ClassSyntaxNode Build(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        var inferredInterfaceName = $"I{current.Type}";
        var type = ((IReadOnlyDictionary<string, Type?>)Helper.PreDefinedInterfaceMap).GetValueOrDefault(inferredInterfaceName, null);
        
        var children = current.Children(nodes);
        var fields = new List<FieldSyntaxNode>();

        var interfaceProperties = type?.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var node in children)
        {
            var fieldTypeNode = ResolveNode(node, nodes);
            var sanitizedName = IdentifierSanitizer.SanitizeName(node.Name);
            fields.Add(new FieldSyntaxNode(
                node.Name,
                sanitizedName,
                node.RequiresAlign(nodes),
                fieldTypeNode,
                true
            ));
            interfaceProperties?.Remove(sanitizedName);
        }

        if (interfaceProperties != null)
        {
            foreach (var remainingProp in interfaceProperties.Values)
            {
                var fieldTypeNode = CreateNodeForMissingField(remainingProp.PropertyType);
                fields.Add(new FieldSyntaxNode(
                    remainingProp.Name,
                    IdentifierSanitizer.SanitizeName(remainingProp.Name),
                    false,
                    fieldTypeNode,
                    false
                ));
            }
        }
        
        
        var interfaceName = type?.Name ?? "IUnityType";
        var concreteName = IdentifierSanitizer.SanitizeName($"{current.Type}_{current.GetHashCode(nodes)}");

        var classSyntaxNode = new ClassSyntaxNode(
            current.Type,
            IdentifierSanitizer.SanitizeName(current.Type),
            current.RequiresAlign(nodes),
            interfaceName,
            concreteName,
            fields.ToArray()
        );
        DiscoveredTypes[current.GetHashCode(nodes)] = classSyntaxNode;
        return classSyntaxNode;
    }

    private TypeDeclarationNode ResolveNode(TypeTreeNode node, List<TypeTreeNode> allNodes)
    {
        var hash = node.GetHashCode(allNodes);
        if (_cache.TryGetValue(hash, out var cachedNode)) return cachedNode;

        TypeDeclarationNode newNode;
        if (Helper.IsPrimitive(node))
        {
            var csharpType = PreDefinedHelper.GetCSharpPrimitiveType(node.Type);
            newNode = new PrimitiveSyntaxNode(node.Type, IdentifierSanitizer.SanitizeName(node.Type), node.RequiresAlign(allNodes), csharpType);
        }
        else if (Helper.IsGenericPPtr(node))
        {
            var genericType = node.Type[5..^1];
            if (genericType == "Object") genericType = "TypeTreeHelper.PreDefined.Types.Object";
            else genericType = Helper.GetPredefinedTypeOrInterfaceName(genericType); // temp solve
            newNode = new PredefinedSyntaxNode(node.Type, $"PPtr<{genericType}>", node.RequiresAlign(allNodes), $"PPtr<{genericType}>");
        }
        else if (Helper.IsPreDefinedType(node))
        {
            newNode = new PredefinedSyntaxNode(node.Type, IdentifierSanitizer.SanitizeName(node.Type), node.RequiresAlign(allNodes), node.Type);
        }
        else if (Helper.IsPair(node))
        {
            var keyNode = node.Children(allNodes)[0];
            var valueNode = node.Children(allNodes)[1];
            var keySyntaxNode = ResolveNode(keyNode, allNodes);
            var valueSyntaxNode = ResolveNode(valueNode, allNodes);
            newNode = new PairSyntaxNode(node.Type, IdentifierSanitizer.SanitizeName(node.Type), node.RequiresAlign(allNodes), keySyntaxNode, valueSyntaxNode);
        }
        else if (Helper.IsVector(node, allNodes))
        {
            var elementNode = node.Children(allNodes)[0].Children(allNodes)[1];
            var elementSyntaxNode = ResolveNode(elementNode, allNodes);
            newNode = new VectorSyntaxNode(node.Type, IdentifierSanitizer.SanitizeName(node.Type), node.RequiresAlign(allNodes), elementSyntaxNode);
        }
        else if (Helper.IsMap(node))
        {
            var pairNode = node.Children(allNodes)[0].Children(allNodes)[1];
            var keyNode = pairNode.Children(allNodes)[0];
            var valueNode = pairNode.Children(allNodes)[1];
            var keySyntaxNode = ResolveNode(keyNode, allNodes);
            var valueSyntaxNode = ResolveNode(valueNode, allNodes);
            var pairSyntaxNode = new PairSyntaxNode(pairNode.Type, IdentifierSanitizer.SanitizeName(pairNode.Type), pairNode.RequiresAlign(allNodes), keySyntaxNode, valueSyntaxNode);
            newNode = new MapSyntaxNode(node.Type, IdentifierSanitizer.SanitizeName(node.Type), node.RequiresAlign(allNodes), pairSyntaxNode);
        }
        else // Complex Type
        {
            var classSyntaxNode = Build(node, allNodes);

            DiscoveredTypes[hash] = classSyntaxNode;
            newNode = classSyntaxNode;
        }

        _cache[hash] = newNode;
        return newNode;
    }

    private TypeDeclarationNode CreateNodeForMissingField(Type csharpType)
    {
        if (csharpType.IsPrimitive || csharpType == typeof(string))
        {
            return new PrimitiveSyntaxNode(csharpType.Name, IdentifierSanitizer.SanitizeName(csharpType.Name), false, csharpType.Name);
        }

        if (csharpType.IsGenericType)
        {
            var genericDef = csharpType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>))
            {
                var elementType = CreateNodeForMissingField(csharpType.GetGenericArguments()[0]);
                return new VectorSyntaxNode("vector", "vector", false, elementType);
            }
            if (genericDef == typeof(KeyValuePair<,>))
            {
                var keyType = CreateNodeForMissingField(csharpType.GetGenericArguments()[0]);
                var valueType = CreateNodeForMissingField(csharpType.GetGenericArguments()[1]);
                return new PairSyntaxNode("pair", "pair", false, keyType, valueType);
            }
        }
        
        // Assume it's a predefined class/struct or another interface
        return new PredefinedSyntaxNode(csharpType.Name, IdentifierSanitizer.SanitizeName(csharpType.Name), false, csharpType.Name);
    }
}
