using System.Reflection;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper;

public static class PreDefinedHelper
{
    private static readonly HashSet<string> _preDefinedTypeNames;
    
    private static readonly Dictionary<string, Type> _preDefinedTypes;
    
    private static readonly HashSet<string> _preDefinedInterfaceNames;
    
    private static readonly Dictionary<string, Type> _preDefinedInterfaces;
    
    static PreDefinedHelper()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        _preDefinedTypes = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedType).IsAssignableFrom(t) && 
                        t.IsClass && !t.IsAbstract)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        _preDefinedInterfaces = assembly
            .GetTypes()
            .Where(t => typeof(IPreDefinedInterface).IsAssignableFrom(t))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        _preDefinedTypeNames = new HashSet<string>(
            _preDefinedTypes.Keys, 
            StringComparer.OrdinalIgnoreCase
        );
        
        _preDefinedInterfaceNames = new HashSet<string>(
            _preDefinedInterfaces.Keys, 
            StringComparer.OrdinalIgnoreCase
        );
    }
    
    public static bool IsPreDefinedType(string unityType) => _preDefinedTypeNames.Contains(unityType);
    
    public static bool IsPreDefinedInterface(string unityType) => _preDefinedInterfaceNames.Contains(unityType);
}