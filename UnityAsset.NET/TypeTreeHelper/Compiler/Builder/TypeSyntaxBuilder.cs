using System.Reflection;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Builder;

public class TypeSyntaxBuilder
{
    private readonly Dictionary<int, TypeDeclarationNode> _cache = new();
    public Dictionary<int, ClassSyntaxNode> DiscoveredTypes { get; } = new();

    private static bool IsNullable(Type csharpType)
    {
        if (!csharpType.IsValueType)
        {
            return true;
        }
        return csharpType.IsGenericType && csharpType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }
    
    private static Dictionary<string, PropertyInfo> GetAllInterfaceProperties(Type type)
    {
        var properties = new Dictionary<string, PropertyInfo>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            properties[property.Name] = property;
        }

        foreach (var baseInterface in type.GetInterfaces())
        {
            foreach (var property in GetAllInterfaceProperties(baseInterface))
            {
                properties[property.Key] = property.Value;
            }
        }

        return properties;
    }

    public ClassSyntaxNode? Build(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (Helper.IsPreDefinedType(current))
            return null;
        var inferredInterfaceName = $"I{current.Type}";
        var type = ((IReadOnlyDictionary<string, Type?>)Helper.PreDefinedInterfaceMap).GetValueOrDefault(inferredInterfaceName, null);
        
        var children = current.Children(nodes);
        var fields = new List<FieldSyntaxNode>();

        var interfaceProperties = type != null ? GetAllInterfaceProperties(type) : null;

        interfaceProperties?.Remove("ClassName");
        
        foreach (var node in children)
        {
            var fieldTypeNode = ResolveNode(node, nodes);
            var sanitizedName = IdentifierSanitizer.SanitizeName(node.Name);
            bool isNullable = interfaceProperties?.TryGetValue(sanitizedName, out var field) == true 
                              && IsNullable(field.PropertyType);
            fields.Add(new FieldSyntaxNode(
                node.Name,
                sanitizedName,
                node.RequiresAlign(nodes),
                fieldTypeNode,
                true,
                isNullable
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
                    false,
                    IsNullable(remainingProp.PropertyType)
                ));
            }
        }
        
        
        var interfaceName = type?.Name ?? (Helper.IsNamedAsset(current, nodes) ? "INamedAsset" : current == nodes[0] ? "IAsset" : "IUnityType");
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

            DiscoveredTypes[hash] = classSyntaxNode!;
            newNode = classSyntaxNode!;
        }

        _cache[hash] = newNode;
        return newNode;
    }

    private TypeDeclarationNode CreateNodeForMissingField(Type csharpType)
    {
        var name = csharpType.IsValueType 
            ? IsNullable(csharpType)
                ? $"{csharpType.GetGenericArguments()[0].Name}"
                : csharpType.Name
            : csharpType.Name;

        if (csharpType.IsPrimitive || csharpType == typeof(string))
        {
            return new PrimitiveSyntaxNode(csharpType.Name, IdentifierSanitizer.SanitizeName(csharpType.Name), false, name);
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
        return new PredefinedSyntaxNode(csharpType.Name, IdentifierSanitizer.SanitizeName(csharpType.Name), false, name);
    }
}
