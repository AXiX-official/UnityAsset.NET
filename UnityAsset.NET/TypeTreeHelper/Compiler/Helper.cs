using System.Reflection;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.PreDefined;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public static class Helper
{
    public static readonly Dictionary<string, Type> PreDefinedInterfaceMap;
    
    public static readonly Dictionary<string, Type> PreDefinedTypeMap;

    static Helper()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        PreDefinedInterfaceMap = assembly
            .GetTypes()
            .Where(t => t.IsInterface && 
                        (t.Namespace == "UnityAsset.NET.TypeTreeHelper.PreDefined.Classes" || 
                         t.Namespace == "UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces"))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        PreDefinedTypeMap = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedType).IsAssignableFrom(t) && 
                        t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsNamedAsset(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        var children = current.Children(nodes);
        if (children.Count == 0) return false;
        // workaround
        return (children[0].Type == "string" &&  children[0].Name == "m_Name");
    }
    
    public static string GetPredefinedTypeOrInterfaceName(string type)
    {
        if (PreDefinedHelper.IsPreDefinedType(type))
            return type;
        switch (type)
        {
            case "AssetBundle": return "IAssetBundle";
            case "Mesh": return "IMesh";
            case "TextAsset": return "ITextAsset";
            case "Texture2D": return "ITexture2D";
            case "Sprite": return "ISprite";
            default:
            {
                var interfaceName = $"I{type}";
                return PreDefinedHelper.IsPreDefinedInterface(interfaceName) ? interfaceName : "IUnityType";
            }
        }
    }
    
    public static string GetCSharpPrimitiveType(string unityType)
    {
        return unityType switch
        {
            "SInt8" => "sbyte",
            "UInt8" => "byte",
            "char" => "char",
            "short" or "SInt16" => "Int16",
            "UInt16" or "unsigned short" => "UInt16",
            "int" or "SInt32" => "Int32",
            "UInt32" or "unsigned int" or "Type*" => "UInt32",
            "long long" or "SInt64" => "Int64",
            "UInt64" or "unsigned long long" or "FileSize" => "UInt64",
            "float" => "float",
            "double" => "double",
            "bool" => "bool",
            "string" => "string",
            _ => throw new NotSupportedException($"Unknown primitive Unity type: {unityType}")
        };
    }
    
    public static bool IsPrimitive(TypeTreeNode current)
    {
        return current.Type switch
        {
            "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or
                "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or
                "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" => true,
            _ => false,
        };
    }
    
    public static bool IsPreDefinedType(TypeTreeNode current) => PreDefinedTypeMap.ContainsKey(current.Type);
    
    public static bool IsVector(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Type == "vector") return true;
        var children = current.Children(nodes);
        if (children.Count == 0) return false;
        if (children[0].Type == "Array") return true;
        return false;
    }

    public static bool IsMap(TypeTreeNode current)
    {
        if (current.Type == "map") return true;
        return false;
    }
    
    public static bool IsPair(TypeTreeNode current)
    {
        return current.Type == "pair";
    }
    
    public static bool IsGenericPPtr(TypeTreeNode current)
    {
        return current.Type.StartsWith("PPtr<") && current.Type.EndsWith(">");
    }
}