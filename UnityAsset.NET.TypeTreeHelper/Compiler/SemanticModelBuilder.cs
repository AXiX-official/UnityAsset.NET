using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.TypeTreeHelper.Compiler.IR;
using UnityAsset.NET.TypeTreeHelper.Model;
using UnityAsset.NET.TypeTreeHelper.Schema;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class SemanticModelBuilder
{
    private readonly Dictionary<int, IUnityTypeInfo> _cache = new();
    public Dictionary<(string Name, int Hash), ClassTypeInfo> DiscoveredTypes { get; } = new();
    private Dictionary<int, string> _cachedGenericTypes = new();
    
    private string GetGenricType(TypeTreeRepr node)
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
    
    public ClassTypeInfo? Build(UnityTypeSchema schema, bool isRootClass = false)
    {
        // The class emitter generates one Unity version per class: the tree its file carries, or the type tree
        // database tree when that file is stripped.
        if (schema.Versions.Count != 1)
            throw new InvalidOperationException(
                $"The runtime generator builds one version per class, but {schema.Name} carries {schema.Versions.Count}.");

        var version = schema.Versions[0];
        var current = version.Tree;

        if (Helper.IsPreDefinedType(current))
            return null;

        // A class with no fields of its own reuses its base's tree, so the tree's root type name can be the base's:
        // a root is identified and looked up by its class name, and the tree name is only the fallback.
        var name = schema.Name;

        bool isGenericType = current.TypeName switch
        {
            "Keyframe" => true,
            "AnimationCurve" => true,
            _ => false
        };
        
        string? inferredInterfaceName = current.TypeName switch
        {
            "Keyframe" => "IKeyframe`1",
            "AnimationCurve" => "IAnimationCurve`1",
            _ => null
        };
        if (inferredInterfaceName is null)
        {
            inferredInterfaceName = $"I{name}";
            if (!InterfaceSchemaTable.Entries.ContainsKey(inferredInterfaceName) && name != current.TypeName)
                inferredInterfaceName = $"I{current.TypeName}";
        }

        // What the interface says comes from the generated table, not from reflecting over the compiled interface.
        var interfaceSchema = InterfaceSchemaTable.Entries.GetValueOrDefault(inferredInterfaceName!);
        var interfaceMembers = interfaceSchema?.Members.ToDictionary(member => member.Name, StringComparer.Ordinal);
        interfaceMembers?.Remove("ClassName");
        
        var fields = new List<UnityFieldInfo>();

        foreach (var member in version.Members)
        {
            var node = member.Node;

            var sanitizedName = Helper.SanitizeName(node.Name);
            bool isOptional = interfaceMembers?.TryGetValue(sanitizedName, out var interfaceMember) == true
                              && interfaceMember.IsOptional;
            
            var fieldTypeInfo = ResolveNode(node);

            var fieldName = Helper.SanitizeName(node.Name); 
            
            fields.Add(new UnityFieldInfo
            {
                Name = fieldName,
                RequireAlign = node.RequiresAlign,
                NeedsNullGuard = NeedsNullGuard(isOptional, isGenericType, interfaceMembers, fieldName, fieldTypeInfo),
                DeclaredTypeSyntax =
                    !isGenericType
                    ? interfaceMembers?.TryGetValue(fieldName, out var declaredMember) ?? false
                    ? SyntaxFactory.ParseTypeName(declaredMember.DeclaredType) : fieldTypeInfo.ToTypeSyntax()
                    : fieldTypeInfo.ToTypeSyntax(),
                TypeInfo = fieldTypeInfo
            });
        }
        
        
        var interfaceName = isRootClass ? "IUnityAsset" : "IUnityObject";

        if (interfaceSchema != null)
        {
            if (current.TypeName == "Keyframe")
            {
                interfaceName = $"IKeyframe<{GetGenricType(current)}>";
            }
            else if (current.TypeName == "AnimationCurve")
            {
                interfaceName = $"IAnimationCurve<{GetGenricType(current)}>";
            }
            else if (Helper.NoInterfaceTypes.Contains(name) || Helper.ClassInterfacesTakenByNestedTypes.Contains(name))
            {
                // Either the type never gets an interface, or a nested structure already owns I<ClassName>: the class
                // is still generated and read, it just does not claim that contract.
                interfaceName = "IUnityObject";
            }
            else
            {
                interfaceName = inferredInterfaceName!;
            }
        }
        
        var generatedClassName = Helper.SanitizeName($"{name}_{current.Hash}");

        var classTypeInfo = new ClassTypeInfo
        {
            Name = name,
            GeneratedClassName = generatedClassName,
            InterfaceName = interfaceName,
            // INamedObject is the contract of a top-level asset type, so a nested type never declares it.
            DeclaresNamedObject = isRootClass && Helper.IsNamedAsset(current) && interfaceSchema?.DerivesFromNamedObject != true,
            Fields = fields,
            TypeTreeRepr = current
        };

        // Identity is the class name plus the structural hash: two classes that share a tree are still two types.
        DiscoveredTypes.TryAdd((name, current.Hash), classTypeInfo);
        return classTypeInfo;
    }

    /// <summary>
    /// A null guard is only valid on a value that can be null, and only meaningful where the interface declares the
    /// member optional. What the interface declares decides it: a "T?" is nullable whatever T is - a version-optional
    /// Vector3f is a Nullable&lt;Vector3f&gt; and must be guarded - and when the declared type is not written with a
    /// "?" the shape of the type info says whether it is a reference one.
    /// </summary>
    private static bool NeedsNullGuard(
        bool isOptional,
        bool isGenericMember,
        Dictionary<string, InterfaceMemberSchema>? interfaceMembers,
        string fieldName,
        IUnityTypeInfo typeInfo)
    {
        if (!isOptional)
            return false;

        // A "T?" on an unconstrained type parameter is only an annotation once that parameter is a value type, so a
        // generic member is judged by the shape it was instantiated with; a concrete "Vector3f?" really is a
        // Nullable<Vector3f> and has to be guarded.
        if (!isGenericMember
            && interfaceMembers?.TryGetValue(fieldName, out var member) == true
            && member.DeclaredType.EndsWith('?'))
            return true;

        return CanBeNull(typeInfo);
    }

    /// <summary>
    /// Whether a member of this shape can be null in C#. A predefined type that is a struct is treated as one that
    /// cannot, which is deliberately conservative: it only matters when the interface did not declare a "?".
    /// </summary>
    private static bool CanBeNull(IUnityTypeInfo typeInfo)
        => typeInfo switch
        {
            PrimitiveTypeInfo primitive => primitive.PrimitiveSyntax.ToString() == "string",
            ClassTypeInfo => true,
            GenericPPtrTypeInfo => true,
            VectorTypeInfo or MapTypeInfo => true, // declared as arrays
            _ => false,
        };

    private IUnityTypeInfo ResolveNode(TypeTreeRepr node)
    {
        var hash = node.GetHashCode();
        if (_cache.TryGetValue(hash, out var cachedInfo)) return cachedInfo;

        IUnityTypeInfo typeInfo;
        if (Helper.IsPrimitive(node))
        {
            var csharpType = Helper.GetCSharpPrimitiveType(node.TypeName);
            typeInfo = new PrimitiveTypeInfo
            {
                TypeTreeRepr = node,
                OriginalTypeName = node.TypeName,
                PrimitiveSyntax = SyntaxFactory.ParseTypeName(csharpType)
            };
        }
        else if (Helper.IsGenericPPtr(node))
        {
            var genericTypeName = node.TypeName.Substring(5, node.TypeName.Length - 6);
            typeInfo = new GenericPPtrTypeInfo
            {
                TypeTreeRepr = node,
                GenericTypeSyntax = SyntaxFactory.ParseTypeName(Helper.GetGenericPPtrInterfaceName(genericTypeName))
            };
        }
        else if (Helper.IsPreDefinedType(node))
        {
            typeInfo = new PredefinedTypeInfo
            {
                TypeTreeRepr = node,
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
                TypeTreeRepr = node,
                Item1Type = ResolveNode(item1Node),
                Item2Type = ResolveNode(item2Node),
                Item1RequireAlign = item1Node.RequiresAlign,
                Item2RequireAlign = item2Node.RequiresAlign
            };
        }
        else if (Helper.IsVector(node))
        {
            var elementNode = node.SubNodes[0].SubNodes[1];
            typeInfo = new VectorTypeInfo
            {
                TypeTreeRepr = node,
                ElementType = ResolveNode(elementNode),
                ElementRequireAlign = elementNode.RequiresAlign
            };
        }
        else if (Helper.IsMap(node))
        {
            var pairNode = node.SubNodes[0].SubNodes[1];
            typeInfo = new MapTypeInfo
            {
                TypeTreeRepr = node,
                PairType = (PairTypeInfo)ResolveNode(pairNode),
                PairRequireAlign = pairNode.RequiresAlign
            };
        }
        else // Complex Type
        {
            typeInfo = Build(UnitySchemaBuilder.Build(node.TypeName, node))!;
        }

        _cache[hash] = typeInfo;
        return typeInfo;
    }
}