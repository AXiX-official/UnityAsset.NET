using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public static class Helper
{
    // Container pseudo-types
    public static HashSet<string> ExcludedBasicTypes =
    [
        "vector",
        "staticvector",
        "set",
        "map",
        "Array",
        "pair",
        "fixed_bitset",
        ""
    ];
    
    // Classes generation handles specially instead of as an ordinary interface.
    public static HashSet<string> ExcludedTypes =
    [
        // workaround for generic type issues
        "Keyframe", // Keyframe should use generic type
        "AnimationCurve", // contains Keyframe
        "GameObject",
        "MonoBehaviour",
        ""
    ];

    // Classes that get a root interface of their own; the application's needs, not the format's.
    public static HashSet<string> IncludedTypes =
    [
        "Animation",
        "Animator",
        "AssetBundle",
        "Shader",
        "Sprite",
        "TextAsset",
        "Texture2D",
        "MeshFilter",
        "SkinnedMeshRenderer"
    ];

    // Classes that must not claim an I<ClassName> of their own, because a nested structure already owns that name.
    //
    // Avatar's human description carries a nested "Collider" structure whose nine members have nothing to do with the
    // Collider class, whose own tree is the single member m_GameObject. A session meets the structure far more often
    // than the class: the structure is what an avatar's collider array holds, and it needs a generated type for that
    // array element. So the structure keeps ICollider and the class gives the name up, falling back to the generic
    // object interface - it is still generated and read, it just does not claim that contract.
    //
    // This is a workaround for one name, not a rule: the classes and the structures live in different namespaces, and
    // giving the structure a scoped name of its own so that both can exist is the proper fix.
    public static HashSet<string> ClassInterfacesTakenByNestedTypes =
    [
        "Collider",
        ""
    ];
    
    // Unity classes a hand-written C# type stands in for.
    public static HashSet<string> PreDefinedTypes =
    [
        "Object",
        "PPtr",
        "StreamingInfo",
        "TypelessData",
        "GameObject",
        "GUID",
        "SpriteAtlas",
        "SpriteAtlasData",
        "Vector2f",
        "Vector3f",
        "Vector4f",
        "Quaternionf",
        "Rectf",
        "SecondarySpriteTexture",
        "BoneWeights4",
        "ChannelInfo",
    ];

    // Predefined stand-ins that are value types. The distinction matters when a member is declared "T?": on a value type
    // that is a Nullable<T> whose payload has to be reached through .Value, while the same "?" on a reference type is
    // only an annotation and the member is read and written directly. TypeTreeHelper cannot ask the type itself - the
    // stand-ins live in the assembly that references this one - so the answer is kept here, where it can be read.
    public static HashSet<string> PreDefinedStructs =
    [
        "ChannelInfo",
        "GUID",
        "Quaternionf",
        "Rectf",
        "Vector2f",
        "Vector3f",
        "Vector4f",
        ""
    ];
    // A nested position uses the hand-written C# type for these instead of a generated interface.
    public static HashSet<string> NoInterfaceTypes =
    [
        "Object",
        "SpriteAtlas",
        "StreamingInfo",
        "TypelessData",
        "GameObject",
        "GUID",
        "EditorSettings", // workaround
        "SpriteAtlasData",
        "Vector2f",
        "Vector3f",
        "Vector4f",
        "Quaternionf", // workaround
        "Rectf",
        "BoneWeights4",
        "ChannelInfo",
    ];

    // PPtr targets that get an interface of their own rather than collapsing to IUnityObject.
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
        "RuntimeAnimatorController",
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
        "NamedObject"
    ];
    
    public static string GetGenericPPtrInterfaceName(string typeName)
    {
        if (typeName.StartsWith('$'))
            typeName = typeName.Substring(1);
        
        if (typeName == "Object")
            return "IUnityObject";
        
        if (NoInterfaceTypes.Contains(typeName))
            return typeName;
        
        if (IncludedPPTrGenricTypes.Contains(typeName))
            return $"I{typeName}";
        
        return "IUnityObject";
    }
    
    public static bool IsNamedAsset(TypeTreeRepr current)
    {
        return current.SubNodes.Any(sb => sb is {TypeName: "string", Name: "m_Name"});
    }

    private const string NamedObjectInterfaceName = "INamedObject";
    
    public static bool DerivesFromNamedObject(Type? interfaceType)
        => interfaceType is not null
           && (interfaceType.Name == NamedObjectInterfaceName
               || interfaceType.GetInterfaces().Any(i => i.Name == NamedObjectInterfaceName));
    
    # region IdentifierSanitizer Logic
    
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

    # endregion

    # region Type Helper Logic

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

    public static bool IsPrimitive(TypeTreeRepr current)
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
            "float" => "ReadSingle",
            "double" => "ReadDouble",
            "bool" => "ReadBoolean",
            "string" => "ReadSizedString",
            _ => throw new NotSupportedException($"No IReader method for Unity type: {unityType}")
        };
    }

    /// <summary>
    /// The writer method that is strictly inverse to <see cref="GetReaderMethodName"/> for the same Unity type: every
    /// leaf rule the reader has has exactly one writer, and the two names differ only in their prefix. Encoding uses
    /// this mapping for the fields it writes.
    /// </summary>
    public static string GetWriterMethodName(string unityType)
    {
        return unityType switch
        {
            "SInt8" => "WriteSByte",
            "UInt8" => "WriteByte",
            "char" => "WriteChar",
            "short" or "SInt16" => "WriteInt16",
            "UInt16" or "unsigned short" => "WriteUInt16",
            "int" or "SInt32" => "WriteInt32",
            "UInt32" or "unsigned int" or "Type*" => "WriteUInt32",
            "long long" or "SInt64" => "WriteInt64",
            "UInt64" or "unsigned long long" or "FileSize" => "WriteUInt64",
            "float" => "WriteSingle",
            "double" => "WriteDouble",
            "bool" => "WriteBoolean",
            "string" => "WriteSizedString",
            _ => throw new NotSupportedException($"No IWriter method for Unity type: {unityType}")
        };
    }

    public static bool IsVector(TypeTreeRepr current)
    {
        if (current.TypeName == "vector" || current.TypeName == "staticvector" || current.TypeName == "set") return true;
        if (current.SubNodes.Length == 0) return false;
        if (current.SubNodes[0].TypeName == "Array") return true;
        return false;
    }

    public static bool IsGenericPPtr(TypeTreeRepr current)
    {
        return current.TypeName.StartsWith("PPtr<") && current.TypeName.EndsWith(">");
    }
    
    public static bool IsPreDefinedType(TypeTreeRepr current) => PreDefinedTypes.Contains(current.TypeName);
    
    public static bool IsMap(TypeTreeRepr current)
    {
        if (current.TypeName == "map") return true;
        return false;
    }

    public static bool IsPair(TypeTreeRepr current)
    {
        return current.TypeName == "pair";
    }

    # endregion

    # region Predefined Interface Logic

    public static bool IsNullable(Type csharpType)
    {
        if (csharpType.IsGenericParameter)
        {
            return false;
        }
        
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

    # endregion

    public static TypeSyntax GetTypeSyntax(Type type)
    {
        if (type.IsArray)
        {
            var elemType = GetTypeSyntax(type.GetElementType()!);
            return SyntaxFactory.ArrayType(elemType)
                .AddRankSpecifiers(
                    SyntaxFactory.ArrayRankSpecifier(
                        SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                            SyntaxFactory.OmittedArraySizeExpression()
                        )
                    )
                );
        }
        
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