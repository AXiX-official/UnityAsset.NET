using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>One member an interface promises, with the type written the way that interface declares it.</summary>
internal readonly struct InterfaceMember
{
    public readonly string Name;
    public readonly string Type;

    public InterfaceMember(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// The checked-in interfaces, keyed by name. This is the only place that reads interface syntax: flattening the members
/// an interface inherits, substituting a generic interface's arguments, and turning an interface name into Unity's
/// class name all live here, so the emitter never has to look at a syntax tree.
/// </summary>
internal sealed class InterfaceModel
{
    private readonly IReadOnlyDictionary<string, InterfaceDeclarationSyntax[]> _definitions;

    /// <summary>The interface names, sorted once so the generated file is stable across runs.</summary>
    private readonly string[] _names;

    public InterfaceModel(IReadOnlyDictionary<string, InterfaceDeclarationSyntax[]> definitions)
    {
        _definitions = definitions;
        _names = definitions.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>The interface names the dispatch table is built from, ordered so the generated file is stable.</summary>
    public IEnumerable<string> Names => _names;

    /// <summary>
    /// Interfaces that get no dispatch arm. They are the roots of the type hierarchy, which the caller never asks the
    /// registry to construct by name, plus the hand-written <c>Renderer</c> stand-in.
    /// </summary>
    private static readonly HashSet<string> NoDispatchArm = new(StringComparer.Ordinal)
    {
        "IUnityObject", "IUnityAsset", "IPreDefinedInterface", "IPreDefinedObject", "INamedObject", "Renderer",
    };

    /// <summary>
    /// Whether the dispatch table gets an arm for this interface. A generic interface has no single Unity class name to
    /// dispatch on - only its instantiations do, which reach the table through the types that mention them.
    /// </summary>
    public bool HasDispatchArm(string interfaceName) =>
        !NoDispatchArm.Contains(interfaceName)
        && !(_definitions.TryGetValue(interfaceName, out var parts) && parts.Any(part => part.TypeParameterList != null));

    /// <summary>Unity's class name for a generated interface: <c>ITexture2D</c> is the interface of <c>Texture2D</c>.</summary>
    public static string ClassName(string interfaceName) =>
        interfaceName.StartsWith("I", StringComparison.Ordinal) ? interfaceName.Substring(1) : interfaceName;

    /// <summary>
    /// Whether this type is one of the interfaces, and therefore gets an adapter class. A generic instantiation such as
    /// <c>IPPtr&lt;GameObject&gt;</c> counts as the interface it instantiates: each instantiation gets its own class,
    /// which is how the generated code monomorphises the generic interfaces.
    /// </summary>
    public bool IsInterface(string type) => _definitions.ContainsKey(BaseName(type));

    /// <summary>The Unity class name a tree has to carry for this type to be read as it.</summary>
    public string SchemaName(string type) => ClassName(BaseName(type));

    /// <summary>
    /// Every member the type promises: its own, then those of the bases it lists. A base that is not a generated
    /// interface contributes nothing, so a hand-written stand-in in a base list is simply skipped.
    /// </summary>
    /// <remarks>
    /// Bases come first and a repeated name keeps its first declaration. That is the order the emitted adapter needs:
    /// a class implements one property per name, and the declaration that wins is the base's.
    /// </remarks>
    public IReadOnlyList<InterfaceMember> Members(string type)
    {
        var name = BaseName(type);
        if (!_definitions.TryGetValue(name, out var parts)) return Array.Empty<InterfaceMember>();

        var arguments = TypeArguments(type);
        var members = new List<InterfaceMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            var parameters = part.TypeParameterList?.Parameters
                .Select(parameter => parameter.Identifier.Text).ToArray() ?? Array.Empty<string>();

            if (part.BaseList is { } baseList)
                foreach (var baseType in baseList.Types)
                    Add(Members(Substitute(baseType.Type, parameters, arguments)));

            foreach (var property in part.Members.OfType<PropertyDeclarationSyntax>())
            {
                // ClassName is the node backend's own property, not something an interface asks for.
                if (property.Identifier.ValueText == "ClassName") continue;
                Add([new InterfaceMember(property.Identifier.ValueText, Substitute(property.Type, parameters, arguments))]);
            }
        }

        return members;

        void Add(IEnumerable<InterfaceMember> candidates)
        {
            foreach (var member in candidates)
                if (seen.Add(member.Name))
                    members.Add(member);
        }
    }

    /// <summary>
    /// Rewrites the member types of a generic interface for one instantiation. The substitution is textual because the
    /// interfaces are read as syntax rather than symbols; the type parameters are the only identifiers a member type can
    /// contain that stand for something to replace.
    /// </summary>
    internal static string Substitute(TypeSyntax type, string[] parameters, string[] arguments)
    {
        if (parameters.Length == 0 || arguments.Length == 0) return type.ToString();

        var result = type;
        foreach (var (parameter, argument) in parameters.Zip(arguments, (parameter, argument) => (parameter, argument)))
            result = result.ReplaceNodes(
                result.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Where(identifier => identifier.Identifier.Text == parameter),
                (_, _) => SyntaxFactory.ParseTypeName(argument));
        return result.ToString();
    }

    /// <summary>
    /// The interface name a generic type instantiates, or the type itself. <c>IPPtr&lt;GameObject&gt;</c> is
    /// <c>IPPtr</c>: one declaration covers every instantiation.
    /// </summary>
    private static string BaseName(string type) =>
        SyntaxFactory.ParseTypeName(type) is GenericNameSyntax generic ? generic.Identifier.Text : type;

    private static string[] TypeArguments(string type) =>
        SyntaxFactory.ParseTypeName(type) is GenericNameSyntax generic
            ? generic.TypeArgumentList.Arguments.Select(argument => argument.ToString()).ToArray()
            : Array.Empty<string>();
}
