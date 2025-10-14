using System.Reflection;
using UnityAsset.NET.TypeTreeHelper.PreDefined;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper;

public static class PreDefinedHelper
{
    private static readonly HashSet<string> PreDefinedTypeNames;
    
    private static readonly HashSet<string> PreDefinedInterfaceNames;
    
    static PreDefinedHelper()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        var preDefinedTypes = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedType).IsAssignableFrom(t) && 
                        t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        var preDefinedInterfaces = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedInterface).IsAssignableFrom(t) ||
                        (t.Namespace?.EndsWith("PreDefined.Classes") ?? false))
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
}