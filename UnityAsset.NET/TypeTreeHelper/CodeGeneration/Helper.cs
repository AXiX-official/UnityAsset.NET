using System.Reflection;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.CodeGeneration;

public static class Helper
{
    private static readonly HashSet<string> PreDefinedTypeNames;
    
    private static readonly HashSet<string> PreDefinedInterfaceNames;
    
    static Helper()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        var preDefinedTypes = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedType).IsAssignableFrom(t) && 
                        t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        var preDefinedInterfaces = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedInterface).IsAssignableFrom(t))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        PreDefinedTypeNames = new HashSet<string>(
            preDefinedTypes.Keys, 
            StringComparer.OrdinalIgnoreCase
        );
        
        PreDefinedInterfaceNames = new HashSet<string>(
            preDefinedInterfaces.Keys, 
            StringComparer.OrdinalIgnoreCase
        );
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

    public static string GetPrimitiveReadLogic(string unityType)
    {
        return unityType switch
        {
            "SInt8" => "reader.ReadInt8()",
            "UInt8" => "reader.ReadUInt8()",
            "char" => "BitConverter.ToChar(reader.ReadBytes(2), 0)",
            "short" or "SInt16" => "reader.ReadInt16()",
            "UInt16" or "unsigned short" => "reader.ReadUInt16()",
            "int" or "SInt32" => "reader.ReadInt32()",
            "UInt32" or "unsigned int" or "Type*" => "reader.ReadUInt32()",
            "long long" or "SInt64" => "reader.ReadInt64()",
            "UInt64" or "unsigned long long" or "FileSize" => "reader.ReadUInt64()",
            "float" => "reader.ReadFloat()",
            "double" => "reader.ReadDouble()",
            "bool" => "reader.ReadBoolean()",
            "string" => "reader.ReadSizedString()",
            _ => throw new NotSupportedException($"Unknown primitive Unity type: {unityType}")
        };
    }
    
    public static bool IsPreDefinedType(string unityType) => PreDefinedTypeNames.Contains(unityType);
    
    public static bool IsPreDefinedInterface(string unityType) => PreDefinedInterfaceNames.Contains(unityType);
    
    public static bool IsPrimitive(string unityType)
    {
        return unityType switch
        {
            "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or
                "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or
                "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" => true,
            _ => false,
        };
    }

    public static bool IsVector(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Type == "vector") return true;
        var children = current.Children(nodes);
        if (children.Count == 0) return false;
        if (children[0].Type == "Array") return true;
        return false;
    }

    public static bool IsMap(TypeTreeNode current, List<TypeTreeNode> nodes)
    {
        if (current.Type == "map") return true;
        return false;
    }

    public static bool IsGenericPPtr(TypeTreeNode current)
    {
        return current.Type.StartsWith("PPtr<") && current.Type.EndsWith(">");
    }
}