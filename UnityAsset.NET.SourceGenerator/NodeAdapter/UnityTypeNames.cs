namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>
/// What a <c>TypeTreeRepr.TypeName</c> has to say for a member to be readable as a given C# type. This is the Unity
/// vocabulary in one place, separate from the code that uses it: the same tables exist for the runtime code generator
/// in <c>TypeTreeHelper/Compiler/Helper</c>, and keeping them together is what makes the duplication visible.
/// </summary>
internal static class UnityTypeNames
{
    /// <summary>The Unity type names a tree may spell a C# type with. Anything not listed matches its own name.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["sbyte"] = ["SInt8"],
            ["byte"] = ["UInt8"],
            ["short"] = ["short", "SInt16"],
            ["ushort"] = ["UInt16", "unsigned short"],
            ["int"] = ["int", "SInt32"],
            ["uint"] = ["UInt32", "unsigned int", "Type*"],
            ["long"] = ["SInt64", "long long"],
            ["ulong"] = ["UInt64", "unsigned long long", "FileSize"],
        };

    /// <summary>An integer type and the equally wide type of the other signedness.</summary>
    public readonly struct SignedPair
    {
        public readonly string Other;
        public readonly string[] UnityNames;

        public SignedPair(string other, string[] unityNames)
        {
            Other = other;
            UnityNames = unityNames;
        }
    }

    /// <summary>
    /// Interface schemas unify the two signednesses of one width, so a tree may spell an <c>int</c> member as
    /// <c>UInt32</c>. The generated code preserves the bits on the way in and writes the schema's own signedness back.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SignedPair> Signedness =
        new Dictionary<string, SignedPair>(StringComparer.Ordinal)
        {
            ["byte"] = new("sbyte", ["SInt8"]),
            ["sbyte"] = new("byte", ["UInt8"]),
            ["short"] = new("ushort", ["UInt16", "unsigned short"]),
            ["ushort"] = new("short", ["short", "SInt16"]),
            ["int"] = new("uint", ["UInt32", "unsigned int", "Type*"]),
            ["uint"] = new("int", ["int", "SInt32"]),
            ["long"] = new("ulong", ["UInt64", "unsigned long long", "FileSize"]),
            ["ulong"] = new("long", ["SInt64", "long long"]),
        };

    /// <summary>
    /// The schema names a value of this type may appear as, in one expression. A primitive matches every alias the tree
    /// may use for it; anything else matches its own schema name, and a generic matches on the part before its
    /// arguments.
    /// </summary>
    public static string Matches(string type, string unityName)
        => Aliases.TryGetValue(type, out var aliases)
            ? Or(aliases)
            : Or([unityName.Split('<')[0]]);

    /// <summary>Matches any pointer, whatever its target: the schema does not carry the target type.</summary>
    public const string AnyPointer = "s.TypeName.StartsWith(\"PPtr<\", StringComparison.Ordinal)";

    /// <summary>
    /// The value types among <see cref="Concrete"/>: a vector of one of these is kept as one flat run of the scalars the
    /// structure is made of, so an adapter reinterprets the array instead of building a node per element.
    /// <para>
    /// Has to agree with <c>PreDefinedTypeTable</c> the same way as <see cref="Concrete"/>: a name here that is not a
    /// value type there makes the generated reinterpretation throw on the first read.
    /// </para>
    /// </summary>
    public static readonly HashSet<string> ConcreteStructs = new(StringComparer.Ordinal)
    {
        "ChannelInfo",
        "GUID",
        "Quaternionf",
        "Rectf",
        "Vector2f",
        "Vector3f",
        "Vector4f",
    };

    /// <summary>
    /// Types the node tree keeps as the value itself, because the catalog builds them from the reader directly. Their
    /// adapter case is a cast in one direction and the value in the other - no encoding step.
    /// <para>
    /// This list has to agree with <c>PreDefinedTypeTable</c> in the library: a name here that the table does not know
    /// makes the adapter cast a tree it never holds, which fails loudly on the first read.
    /// </para>
    /// </summary>
    public static readonly HashSet<string> Concrete = new(StringComparer.Ordinal)
    {
        "BoneWeights4",
        "ChannelInfo",
        "GUID",
        "Quaternionf",
        "Rectf",
        "SecondarySpriteTexture",
        "SpriteAtlasData",
        "StreamingInfo",
        "TypelessData",
        "Vector2f",
        "Vector3f",
        "Vector4f",
    };

    /// <summary>
    /// Whether a vector of this element type is kept as one typed array rather than as a node per element. It has to
    /// agree with <c>NodeData.IsRun</c>: those are the values the node tree reads and writes as a run.
    /// </summary>
    public static bool IsRunElement(string elementType) => elementType is
        "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double"
        or "string";

    /// <summary>Matches one exact schema name.</summary>
    public static string Exact(string unityName) => $"s.TypeName == \"{unityName}\"";

    /// <summary>Joins schema-name tests with <c>||</c>.</summary>
    public static string Or(IEnumerable<string> unityNames) =>
        string.Join(" || ", unityNames.Select(name => $"s.TypeName == \"{name}\""));
}
