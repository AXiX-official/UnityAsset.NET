using System.Reflection;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Extensions;

public static class ObjectExtensions
{
    public static T GetPropertyByOriginalName<T>(this object obj, string originalName)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        
        var property = obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<OriginalNameAttribute>()?.Name == originalName);
        
        if (property == null)
            throw new ArgumentException($"Property with OriginalName '{originalName}' not found");
        
        var value = property.GetValue(obj);
        if (value == null)
            throw new NullReferenceException($"Property '{originalName}' value is null");
        
        if (value is T typedValue)
            return typedValue;
        
        throw new InvalidCastException($"Property '{originalName}' is not of type {typeof(T).Name}");
    }
}