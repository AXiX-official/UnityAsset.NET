using System.Reflection;

namespace UnityAsset.NET.TypeTreeHelper;

public static class UnityTypeHelper
{
    public static string GetClassName(Type type)
    {
        var attribute = type.GetCustomAttribute<OriginalNameAttribute>();
        return attribute?.Name ?? type.Name;
    }
}
