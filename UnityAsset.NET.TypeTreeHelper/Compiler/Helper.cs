using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public static class Helper
{
    public static HashSet<string> ExcludedTypes =
    [
        "vector",
        "map",
        "Array",
        "pair",
        // workaround for generic type issues
        "Keyframe", // Keyframe should use generic type
        "AnimationCurve", // contains Keyframe
        ""
    ];
    
    public static HashSet<string> PreDefinedTypes =
    [
        "Object",
        "PPtr",
        "StreamingInfo",
        "TypelessData",
        "GUID",
        "SpriteAtlas",
        "SpriteAtlasData",
        "Vector2f",
        "Vector3f",
        "Vector4f",
        "Quaternionf",
        "Rectf",
        "SecondarySpriteTexture"
    ];

    public static HashSet<string> NoInterfaceTypes =
    [
        "Object",
        //"SpriteAtlas",
        "StreamingInfo",
        "TypelessData",
        "EditorSettings", // workaround
        "Quaternionf", // workaround
    ];

    public static HashSet<string> IncludedPPTrGenricTypes =
    [
        "GameObject",
        "Transform",
        "RenderTexture",
        "Shader",
        "Material",
        "Mesh",
        "OcclusionCullingData",
        "Renderer",
        "OcclusionPortal",
        "ShaderVariantCollection",
        "MonoBehaviour",
        "Object",
        //"Texture",
        "PhysicMaterial",
        "PhysicsMaterial",
        "Rigidbody",
        "ArticulationBody",
        //"urv",
        "AudioMixerGroup",
        "AudioClip",
        "AudioResource",
        "Texture2D",
        "Font",
        "Cubemap",
        "Light",
        "Flare",
        "AnimationClip",
        "MonoScript",
        "Sprite",
        "VulkanDeviceFilterLists",
        "D3D12DeviceFilterLists",
        "Component",
        "TerrainData",
        "LightProbes",
        "LightingSettings",
        "SceneAsset",
        "CapsuleCollider",
        "SphereCollider",
        "ProceduralTexture",
        "SubstanceArchive",
        "ProceduralMaterial",
        "SpeedTreeWindAsset",
        "Prefab",
        "Avatar",
        "NavMeshData",
        //"RuntimeAnimatorController",
        "AnimatorStateMachine",
        "AnimatorState",
        "AnimatorStateTransition",
        //"Motion",
        "AnimatorTransition",
        "PhysicsMaterial2D",
        "SpriteAtlas",
        "Rigidbody2D",
        "Camera",
        "BillboardAsset",
        "AudioMixerSnapshot",
        "AudioMixer",
        "AudioMixerEffectController",
        "VideoClip",
        "AudioSource",
        "ComputeShader",
        "VisualEffectAsset",
        "BrokenPrefabAsset",
        "AudioContainerElement",
        "BlobObject",
        "SortingGroup",
        "BlockShaderContainer",
        //"eyfram",
        "TerrainLayer",
        "AvatarMask",
        "MeshRenderer",
        "SkinnedMeshRenderer",
        "SpriteRenderer",
        "ParticleSystemForceField",
        "ParticleSystem",
        "Collider2D",
        "Preset",
        "Texture3D",
        //"NamedObject"
    ];
    
    public static Dictionary<string, int> PrimitiveNumericRankings = new()
    {
        { "bool", 0 },
        { "byte", 1 },
        { "sbyte", 1 },
        { "char", 1 },
        { "short", 2 },
        { "ushort", 2 },
        { "int", 3 },
        { "uint", 3 },
        { "long", 4 },
        { "ulong", 4 },
        { "float", 5 },
        { "double", 6 }
    };
    
    public static List<string> PrimitiveNumericMap = ["bool", "byte", "short", "int", "long", "float", "double"];
    
    public static string GetGenericPPtrInterfaceName(string typeName)
    {
        if (typeName == "Object")
            return "UnityObject";
        
        if (NoInterfaceTypes.Contains(typeName))
            return typeName;
        
        if (IncludedPPTrGenricTypes.Contains(typeName))
            return $"I{typeName}";
        
        return "IUnityType";
    }
    
    public static string GetInterfaceName(TypeTreeNode node)
    {
        if (IsPrimitive(node.TypeName))
            return node.TypeName;
        
        if (NoInterfaceTypes.Contains(node.TypeName))
            return "IUnityType";
    
        if (node.TypeName.StartsWith("PPtr"))
            return $"PPtr<{GetGenericPPtrInterfaceName(node.TypeName.Substring(5, node.TypeName.Length - 6))}>";
    
        if (node.TypeName == "pair")
            return $"ValueTuple<{GetInterfaceName(node.SubNodes[0])}, {GetInterfaceName(node.SubNodes[1])}>";

        if (node.TypeName == "vector" || node.TypeName == "map")
            return GetInterfaceName(node.SubNodes[0]);
    
        if (node.TypeName == "Array")
            return $"List<{GetInterfaceName(node.SubNodes[1])}>";
    
        return $"I{node.TypeName}";
    }
    
    public static bool IsNamedAsset(TypeTreeNode current)
    {
        return current.SubNodes.Any(sb => sb is {TypeName: "string", Name: "m_Name"});
    }
    
    #region IdentifierSanitizer Logic
    
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
        
        return SyntaxFacts.IsReservedKeyword(SyntaxFacts.GetKeywordKind(fixedName)) ? "@" + fixedName : fixedName;
    }

    #endregion

    #region Type Helper Logic

    public static string GetCSharpPrimitiveType(string unityType)
    {
        return unityType switch
        {
            "SInt8" => "sbyte",
            "UInt8" => "byte",
            "char" => "char",
            "short" or "SInt16" => "short",
            "UInt16" or "unsigned short" => "ushort",
            "int" or "SInt32" => "int",
            "UInt32" or "unsigned int" or "Type*" => "uint",
            "long long" or "SInt64" => "long",
            "UInt64" or "unsigned long long" or "FileSize" => "ulong",
            "float" => "float",
            "double" => "double",
            "bool" => "bool",
            "string" => "string",
            _ => throw new NotSupportedException($"Unknown primitive Unity type: {unityType}")
        };
    }
    
    public static bool IsCSharpPrimitive(string type)
    {
        return type switch
        {
            "sbyte" or "byte" or "char" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
                "float" or "double" or "bool" or "string" => true,
            _ => false,
        };
    }

    public static bool IsPrimitive(TypeTreeNode current)
    {
        return IsPrimitive(current.TypeName);
    }
    
    public static bool IsPrimitive(string type)
    {
        return type switch
        {
            "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or
                "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or
                "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" => true,
            _ => false,
        };
    }

    public static string GetReaderMethodName(string unityType)
    {
        return unityType switch
        {
            "SInt8" => "ReadSByte",
            "UInt8" => "ReadByte",
            "char" => "ReadChar",
            "short" or "SInt16" => "ReadInt16",
            "UInt16" or "unsigned short" => "ReadUInt16",
            "int" or "SInt32" => "ReadInt32",
            "UInt32" or "unsigned int" or "Type*" => "ReadUInt32",
            "long long" or "SInt64" => "ReadInt64",
            "UInt64" or "unsigned long long" or "FileSize" => "ReadUInt64",
            "float" => "ReadFloat",
            "double" => "ReadDouble",
            "bool" => "ReadBoolean",
            "string" => "ReadSizedString",
            _ => throw new NotSupportedException($"No IReader method for Unity type: {unityType}")
        };
    }

    public static bool IsVector(TypeTreeNode current)
    {
        if (current.TypeName == "vector") return true;
        if (current.SubNodes.Length == 0) return false;
        if (current.SubNodes[0].TypeName == "Array") return true;
        return false;
    }

    public static bool IsGenericPPtr(TypeTreeNode current)
    {
        return current.TypeName.StartsWith("PPtr<") && current.TypeName.EndsWith(">");
    }
    
    public static bool IsPreDefinedType(TypeTreeNode current) => PreDefinedTypes.Contains(current.TypeName);
    
    public static bool IsMap(TypeTreeNode current)
    {
        if (current.TypeName == "map") return true;
        return false;
    }

    public static bool IsPair(TypeTreeNode current)
    {
        return current.TypeName == "pair";
    }

    #endregion

    #region Predefined Interface Logic

    public static bool IsNullable(Type csharpType)
    {
        if (!csharpType.IsValueType)
        {
            return true;
        }
        return csharpType.IsGenericType && csharpType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public static Dictionary<string, PropertyInfo> GetAllInterfaceProperties(Type type)
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

    #endregion

    public static TypeSyntax GetTypeSyntax(Type type)
    {
        /*if (type.FullName == "UnityAsset.NET.TypeTreeHelper.PreDefined.Types.Object")
        {
            return SyntaxFactory.ParseTypeName("global::UnityAsset.NET.TypeTree.PreDefined.Types.Object");
        }*/
        
        if (type.IsGenericType)
        {
            var genericBaseName = type.Name.Split('`')[0];
            if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return type.GetGenericArguments().Select(GetTypeSyntax).First();
            var typeArgumentSyntaxes = type.GetGenericArguments().Select(GetTypeSyntax);
            return SyntaxFactory.GenericName(
                SyntaxFactory.Identifier(genericBaseName)
            )
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(typeArgumentSyntaxes)
                )
            );
        }

        return SyntaxFactory.ParseTypeName(type.Name);
    }
}
