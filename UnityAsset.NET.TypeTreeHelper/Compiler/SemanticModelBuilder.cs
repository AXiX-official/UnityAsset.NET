using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.TypeTreeHelper.Compiler.IR;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class SemanticModelBuilder
{
    private readonly Dictionary<int, IUnityTypeInfo> _cache = new();
    public Dictionary<int, ClassTypeInfo> DiscoveredTypes { get; } = new();
    private readonly Dictionary<string, Type> _preDefinedInterfaceMap;
    private Dictionary<int, string> _cachedGenericTypes = new();

    public SemanticModelBuilder(Dictionary<string, Type> preDefinedInterfaceMap)
    {
        _preDefinedInterfaceMap = preDefinedInterfaceMap;
    }
    
    private string GetGenricType(TypeTreeNode node)
    {
        if (_cachedGenericTypes.TryGetValue(node.Hash, out var type))
        {
            return type;
        }

        if (node.TypeName == "Keyframe")
        {
            type = node.SubNodes[1].TypeName;
            _cachedGenericTypes[node.Hash] = type;
            return type;
        }
        
        if (node.TypeName == "AnimationCurve")
        {
            type = GetGenricType(node.SubNodes[0].SubNodes[0].SubNodes[1]); // keyframe<T> m_Curve
            _cachedGenericTypes[node.Hash] = type;
            return type;
        }

        type = string.Empty;
        _cachedGenericTypes[node.Hash] = type;
        return type;
    }
    
    public ClassTypeInfo? Build(TypeTreeNode current)
    {
        if (Helper.IsPreDefinedType(current))
            return null;
        
        bool isGenericType = current.TypeName switch
        {
            "Keyframe" => true,
            "AnimationCurve" => true,
            _ => false
        };
        
        var inferredInterfaceName = current.TypeName switch
        {
            "Keyframe" => "IKeyframe`1",
            "AnimationCurve" => "IAnimationCurve`1",
            _ => $"I{current.TypeName}"
        };
        var type = ((IReadOnlyDictionary<string, Type?>)_preDefinedInterfaceMap).GetValueOrDefault(inferredInterfaceName, null);
        
        var fields = new List<UnityFieldInfo>();

        var interfaceProperties = type != null ? Helper.GetAllInterfaceProperties(type) : null;

        interfaceProperties?.Remove("ClassName");
        
        foreach (var node in current.SubNodes)
        {
            
            var sanitizedName = Helper.SanitizeName(node.Name);
            bool isNullable = interfaceProperties?.TryGetValue(sanitizedName, out var field) == true 
                              && Helper.IsNullable(field.PropertyType);
            
            var fieldTypeInfo = ResolveNode(node);

            var fieldName = Helper.SanitizeName(node.Name); 
            
            fields.Add(new UnityFieldInfo
            {
                Name = fieldName,
                RequireAlign = node.RequiresAlign(),
                IsNullable = isNullable,
                DeclaredTypeSyntax =
                    !isGenericType
                    ? interfaceProperties?.TryGetValue(fieldName, out var interfaceProperty) ?? false
                    ? Helper.GetTypeSyntax(interfaceProperty.PropertyType) : fieldTypeInfo.ToTypeSyntax()
                    : fieldTypeInfo.ToTypeSyntax(),
                TypeInfo = fieldTypeInfo
            });
        }
        
        
        //var interfaceName = type?.Name ?? (Helper.IsNamedAsset(current) ? "INamedAsset" : current == nodes[0] ? "IAsset" : "IUnityType");
        var interfaceName = "IUnityType";

        if (type != null)
        {
            if (current.TypeName == "Keyframe")
            {
                interfaceName = $"IKeyframe<{GetGenricType(current)}>";
            }
            else if (current.TypeName == "AnimationCurve")
            {
                interfaceName = $"IAnimationCurve<{GetGenricType(current)}>";
            }
            else
            {
                interfaceName = Helper.GetInterfaceName(current);
            }
        }
        
        var generatedClassName = Helper.SanitizeName($"{current.TypeName}_{current.Hash}");

        var classTypeInfo = new ClassTypeInfo
        {
            Name = current.TypeName,
            GeneratedClassName = generatedClassName,
            InterfaceName = interfaceName,
            Fields = fields,
            TypeTreeNode = current
        };
        
        DiscoveredTypes[current.Hash] = classTypeInfo;
        return classTypeInfo;
    }

    private IUnityTypeInfo ResolveNode(TypeTreeNode node)
    {
        var hash = node.Hash;
        if (_cache.TryGetValue(hash, out var cachedInfo)) return cachedInfo;

        IUnityTypeInfo typeInfo;
        if (Helper.IsPrimitive(node))
        {
            var csharpType = Helper.GetCSharpPrimitiveType(node.TypeName);
            typeInfo = new PrimitiveTypeInfo
            {
                TypeTreeNode = node,
                OriginalTypeName = node.TypeName,
                PrimitiveSyntax = SyntaxFactory.ParseTypeName(csharpType)
            };
        }
        else if (Helper.IsGenericPPtr(node))
        {
            var genericTypeName = node.TypeName.Substring(5, node.TypeName.Length - 6);
            typeInfo = new GenericPPtrTypeInfo
            {
                TypeTreeNode = node,
                GenericTypeSyntax = SyntaxFactory.ParseTypeName(Helper.GetGenericPPtrInterfaceName(genericTypeName))
            };
        }
        else if (Helper.IsPreDefinedType(node))
        {
            typeInfo = new PredefinedTypeInfo
            {
                TypeTreeNode = node,
                PredefinedTypeSyntax = SyntaxFactory.ParseTypeName(Helper.SanitizeName(node.TypeName))
            };
        }
        else if (Helper.IsPair(node))
        {
            var children = node.SubNodes;
            var item1Node = children[0];
            var item2Node = children[1];
            typeInfo = new PairTypeInfo
            {
                TypeTreeNode = node,
                Item1Type = ResolveNode(item1Node),
                Item2Type = ResolveNode(item2Node),
                Item1RequireAlign = item1Node.RequiresAlign(),
                Item2RequireAlign = item2Node.RequiresAlign()
            };
        }
        else if (Helper.IsVector(node))
        {
            var elementNode = node.SubNodes[0].SubNodes[1];
            typeInfo = new VectorTypeInfo
            {
                TypeTreeNode = node,
                ElementType = ResolveNode(elementNode),
                ElementRequireAlign = elementNode.RequiresAlign()
            };
        }
        else if (Helper.IsMap(node))
        {
            var pairNode = node.SubNodes[0].SubNodes[1];
            typeInfo = new MapTypeInfo
            {
                TypeTreeNode = node,
                PairType = (PairTypeInfo)ResolveNode(pairNode),
                PairRequireAlign = pairNode.RequiresAlign()
            };
        }
        else // Complex Type
        {
            var classTypeInfo = Build(node);

            DiscoveredTypes[hash] = classTypeInfo!;
            typeInfo = classTypeInfo!;
        }

        _cache[hash] = typeInfo;
        return typeInfo;
    }
}