using System.Reflection;
using System.Runtime.CompilerServices;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.Types.PreDefined;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Types;

/// <summary>Copies the stored object graph, including a union's discriminant, without decoding it again.</summary>
public static class AssetCloner
{
    private static readonly ConditionalWeakTable<IUnityObject, SerializedType> Origins = new();
    internal static void RecordOrigin(IUnityObject value, SerializedType type) => Origins.AddOrUpdate(value, type);
    internal static SerializedType? Origin(IUnityObject value) => Origins.TryGetValue(value, out var type) ? type : null;
    private static readonly MethodInfo CloneMethod = typeof(object).GetMethod(
        "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static T CopyField<T>(T value)
        => (T)Copy(value, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;

    public static T Clone<T>(T original) where T : IUnityObject
    {
        var clone = (T)Copy(original, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
        if (Origin(original) is { } type) RecordOrigin(clone, type);
        return clone;
    }

    private static object? Copy(object? value, Dictionary<object, object> copies)
    {
        if (value is null) return null;
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or decimal)
            return value;
        // References keep their file identity; following them would clone the entire owning session.
        if (value is IPPtr or TypeTreeRepr)
            return value;
        if (copies.TryGetValue(value, out var existing)) return existing;
        if (value is Array array)
        {
            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                throw new NotSupportedException("Only zero-based asset vectors can be cloned.");
            var result = (Array)array.Clone();
            copies.Add(value, result);
            for (var i = 0; i < array.Length; i++)
                result.SetValue(Copy(array.GetValue(i), copies), i);
            return result;
        }
        if (value is System.Collections.IDictionary dictionary)
        {
            var result = (System.Collections.IDictionary)Activator.CreateInstance(type)!;
            copies.Add(value, result);
            foreach (System.Collections.DictionaryEntry entry in dictionary)
                result.Add(Copy(entry.Key, copies)!, Copy(entry.Value, copies));
            return result;
        }
        if (value is System.Collections.IList list)
        {
            var result = (System.Collections.IList)Activator.CreateInstance(type)!;
            copies.Add(value, result);
            foreach (var item in list) result.Add(Copy(item, copies));
            return result;
        }
        if (!type.IsValueType && value is not IUnityObject && value is not NodeData)
            throw new NotSupportedException($"No asset clone contract for {type.FullName}.");

        var clone = CloneMethod.Invoke(value, null)!;
        copies.Add(value, clone);
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                field.SetValue(clone, field.Name == "__assetEditable" ? true : Copy(field.GetValue(value), copies));
        }
        return clone;
    }
}
