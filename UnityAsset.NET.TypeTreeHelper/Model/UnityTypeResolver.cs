using UnityAsset.NET.TypeTreeHelper.Compiler;

namespace UnityAsset.NET.TypeTreeHelper.Model;

/// <summary>
/// The rules that turn a type tree into the C# type a generated interface declares for it, and the rule that merges
/// the types one member has across versions. The offline emitter still has its own copy of these while C1 has not
/// taken over; <see cref="UnitySchemaVerifier"/> keeps the two honest in the meantime.
/// </summary>
public static class UnityTypeResolver
{
    private static readonly Dictionary<(string, string), string> NumericUnifyMap = new()
    {
        { ("sbyte", "byte"), "byte" },
        { ("byte", "sbyte"), "byte" },
        { ("short", "ushort"), "ushort" },
        { ("ushort", "short"), "ushort" },
        { ("int", "uint"), "uint" },
        { ("uint", "int"), "uint" },
        { ("long", "ulong"), "ulong" },
        { ("ulong", "long"), "ulong" },
    };

    /// <summary>The C# type one member node is generated as.</summary>
    public static string GetTypeName(TypeTreeRepr node)
    {
        if (Helper.IsPrimitive(node.TypeName))
            return Helper.GetCSharpPrimitiveType(node.TypeName);

        if (node.TypeName.StartsWith("PPtr<"))
        {
            var genericType = node.TypeName.Substring(5, node.TypeName.Length - 6);
            return genericType == "Object"
                ? "PPtr<IUnityObject>"
                : Helper.IncludedPPTrGenricTypes.Contains(genericType)
                    ? Helper.NoInterfaceTypes.Contains(genericType)
                        ? $"PPtr<{genericType}>"
                        : $"PPtr<I{genericType}>"
                    : "PPtr<IUnityObject>";
        }

        if (node.TypeName == "pair")
            return $"ValueTuple<{GetTypeName(node.SubNodes[0])}, {GetTypeName(node.SubNodes[1])}>";

        // Containers are recognized the way the reader recognizes them: by name as before, and by shape - a wrapper
        // whose only content is an Array, which is how a fixed-length bitset (fixed_bitset) is stored. The two sides
        // have to agree, or a member is declared as a type the reader never builds. A string has that same shape and
        // is excluded by name: it is the only leaf that does, and it is serialized as an Array of chars.
        if (node.TypeName != "string" && (Helper.IsVector(node) || node.TypeName == "map"))
            return GetTypeName(node.SubNodes[0]);

        if (node.TypeName == "Array")
            return $"{GetTypeName(node.SubNodes[1])}[]";

        if (Helper.PreDefinedTypes.Contains(node.TypeName))
            return node.TypeName;

        var genericTypeName = GetGenericTypeName(node);
        return genericTypeName.Length == 0 ? $"I{node.TypeName}" : $"I{node.TypeName}<{genericTypeName}>";
    }

    /// <summary>
    /// The one type a member has across versions: itself when every version agrees, the wider of two same-width
    /// numeric types, and a RefSum when they genuinely differ.
    /// </summary>
    public static string GetUnionType(IReadOnlyList<string> types)
    {
        var uniqueTypes = types.Distinct().ToList();

        if (uniqueTypes.Count == 1)
            return types[0];

        if (uniqueTypes.Count == 2 && NumericUnifyMap.TryGetValue((uniqueTypes[0], uniqueTypes[1]), out var unified))
            return unified;

        return $"RefSum<{string.Join(", ", uniqueTypes)}>";
    }

    /// <summary>The type argument of a generic Unity type, empty when the type does not spread a generic one.</summary>
    private static string GetGenericTypeName(TypeTreeRepr node)
    {
        if (node.TypeName == "Keyframe")
            return node.SubNodes[1].TypeName;

        if (node.TypeName == "AnimationCurve")
            return GetGenericTypeName(node.SubNodes[0].SubNodes[0].SubNodes[1]); // keyframe<T> m_Curve

        // assert we can cut generic type spreading here
        return string.Empty;
    }
}
