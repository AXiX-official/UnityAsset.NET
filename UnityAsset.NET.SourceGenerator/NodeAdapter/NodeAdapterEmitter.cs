using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static UnityAsset.NET.SourceGenerator.NodeAdapter.GeneratedNames;

namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>
/// Turns the interfaces into the generated file. Emission is two passes: <see cref="Resolve"/> works out which types the
/// file has to mention - the work list grows as shapes are described - and then the passes over that closed list
/// append the classes and the functions. Nothing is written while the set of types is still being decided, so the
/// structure of the output is readable from <see cref="Emit"/> alone.
/// </summary>
internal sealed class NodeAdapterEmitter
{
    private const string GeneratedBy = "UnityAsset.NET.SourceGenerator.NodeAdapterGenerator";

    private readonly InterfaceModel _model;
    private readonly NodeAdapterShape _shape;
    private readonly StringBuilder _code = new();

    /// <summary>Every type the file mentions, in the order it was first reached.</summary>
    private readonly List<GeneratedType> _types = new();

    /// <summary>The interfaces the dispatch table names, in emission order, with their identifiers.</summary>
    private readonly List<DispatchArm> _dispatch = new();

    /// <summary>The emitted identifier for each type, keyed by the type as the interfaces spell it.</summary>
    private readonly Dictionary<string, string> _identifiers = new(StringComparer.Ordinal);

    private readonly HashSet<string> _usedIdentifiers = new(StringComparer.Ordinal);
    private readonly Queue<string> _pending = new();

    public NodeAdapterEmitter(InterfaceModel model)
    {
        _model = model;
        _shape = new NodeAdapterShape(model, Register);
    }

    public string Emit()
    {
        Resolve();
        AppendHeader();
        AppendDispatch();
        foreach (var type in _types)
            if (type.IsAdapter) AppendAdapter(type);
        foreach (var type in _types) AppendFunctions(type);
        return _code.Append("}\n").ToString();
    }

    /// <summary>
    /// Closes the set of types to emit. Registering a type is what queues it, and describing a shape registers the types
    /// it is made of, so the loop drains only when every type the interfaces reach has been described.
    /// </summary>
    private void Resolve()
    {
        // Only the interfaces the table names are seeded: a generic interface on its own has no class name to
        // dispatch on, so nothing may be emitted for the bare declaration.
        foreach (var interfaceName in _model.Names)
        {
            if (!_model.HasDispatchArm(interfaceName)) continue;
            _dispatch.Add(new DispatchArm(interfaceName, Register(interfaceName)));
        }

        while (_pending.Count > 0)
        {
            var type = _pending.Dequeue();
            var isAdapter = _model.IsInterface(type);
            _types.Add(new GeneratedType(type, _identifiers[type], _shape.Case(type), isAdapter,
                isAdapter ? AdapterMembers(type) : Array.Empty<GeneratedMember>()));
        }
    }

    /// <summary>The properties an adapter class implements, with the identifiers their generated functions have.</summary>
    private IReadOnlyList<GeneratedMember> AdapterMembers(string type)
    {
        var members = _model.Members(type);
        var generated = new GeneratedMember[members.Count];
        for (var index = 0; index < members.Count; index++)
            generated[index] = new GeneratedMember(members[index].Name, members[index].Type, Register(members[index].Type));
        return generated;
    }

    /// <summary>The identifier for a type, queueing it if this is the first time it is asked for.</summary>
    private string Register(string type)
    {
        if (_identifiers.TryGetValue(type, out var identifier)) return identifier;

        identifier = UniqueIdentifier(type);
        _identifiers.Add(type, identifier);
        _pending.Enqueue(type);
        return identifier;
    }

    /// <summary>
    /// A readable identifier for a type, so the generated file and its stack traces name what they mean instead of
    /// carrying an index that has to be looked up. Very long names are shortened and disambiguated by a hash.
    /// </summary>
    private string UniqueIdentifier(string type)
    {
        var stem = Describe(type);
        if (stem.Length > 64) stem = stem.Substring(0, 48) + "_" + StableHash(type);

        var name = stem;
        for (var suffix = 2; !_usedIdentifiers.Add(name); suffix++) name = stem + "_" + suffix;
        return name;
    }

    private static string Describe(string type) => Describe(SyntaxFactory.ParseTypeName(type));

    private static string Describe(TypeSyntax syntax) => syntax switch
    {
        NullableTypeSyntax nullable => Describe(nullable.ElementType) + "_Nullable",
        ArrayTypeSyntax array => Describe(array.ElementType) + string.Concat(
            Enumerable.Repeat("_Array", array.RankSpecifiers.Count)),
        GenericNameSyntax generic => Ident(generic.Identifier.Text) + "_" + string.Join("_",
            generic.TypeArgumentList.Arguments.Select(Describe)),
        _ => Ident(syntax.ToString()),
    };

    /// <summary>Everything that cannot appear in an identifier becomes an underscore, digits cannot lead.</summary>
    private static string Ident(string text)
    {
        var builder = new StringBuilder(text.Length + 1);
        foreach (var character in text)
            builder.Append(IsIdentifierCharacter(character) ? character : '_');
        if (builder.Length == 0) builder.Append('_');
        if (char.IsDigit(builder[0])) builder.Insert(0, '_');
        return builder.ToString();
    }

    private static bool IsIdentifierCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    /// <summary>FNV-1a, so a shortened name stays the same across runs and machines.</summary>
    private static string StableHash(string text)
    {
        var hash = 2166136261u;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash.ToString("x8");
    }

    private void AppendHeader()
    {
        _code.Append("// <auto-generated />\n");
        _code.Append("// ").Append(_types.Count).Append(" types adapted from ")
            .Append(_model.Names.Count()).Append(" interfaces by ").Append(GeneratedBy).Append(".\n");
        _code.Append("#nullable enable\n");
        _code.Append("using System;\n");
        _code.Append("using System.Linq;\n");
        _code.Append("using UnityAsset.NET.IO;\n");
        _code.Append("using UnityAsset.NET.TypeTreeHelper;\n");
        _code.Append("using UnityAsset.NET.Types.PreDefined;\n");
        _code.Append("using UnityAsset.NET.Types.PreDefined.Types;\n");
        _code.Append("using UnityAsset.NET.Types.PreDefined.Interfaces;\n");
        _code.Append("namespace UnityAsset.NET.Types;\n");
        _code.Append("internal static class NodeAdapters {\n");
    }

    /// <summary>The one place a Unity class name is turned into an adapter.</summary>
    private void AppendDispatch()
    {
        _code.Append("internal static IUnityAsset Create(string name, object v, TypeTreeRepr s, IReader r) => name switch {\n");
        foreach (var arm in _dispatch)
            _code.Append('"').Append(InterfaceModel.ClassName(arm.InterfaceName)).Append("\" => new ")
                .Append(Adapter(arm.Identifier)).Append("(v,s,r),\n");

        // Anything the interfaces do not cover keeps the schema-based object, named when the tree has a name.
        _code.Append("_ => s.IsNamed ? new NamedNodeObject(v,s,r) : new NodeObject(v,s,r) };\n");
    }

    private void AppendAdapter(GeneratedType type)
    {
        _code.Append("private sealed class ").Append(Adapter(type.Identifier))
            .Append("(object v, TypeTreeRepr s, IReader r) : NodeObject(v,s,r), ").Append(type.Type).Append(" {\n");

        // Names once per type, then a slot resolved once per schema: a read used to hash a member name on every call, and
        // on real data that lookup was most of what a typed read cost once the value was cached.
        _code.Append("private static readonly string[] MemberNames = [");
        for (var position = 0; position < type.Members.Count; position++)
        {
            if (position > 0) _code.Append(", ");
            _code.Append('"').Append(type.Members[position].Name).Append('"');
        }

        _code.Append("];\n");
        _code.Append("private static SlotMap? Slots;\n");
        _code.Append("private int Slot(int position) {\n");
        _code.Append("    var slots = Slots;\n");
        _code.Append("    if (slots is null || !ReferenceEquals(slots.Schema, Schema))\n");
        _code.Append("        Slots = slots = MapOf(Schema, MemberNames);\n");
        _code.Append("    return slots.Positions[position];\n");
        _code.Append("}\n");

        for (var position = 0; position < type.Members.Count; position++)
        {
            var member = type.Members[position];
            _code.Append("public ").Append(member.Type).Append(" @").Append(member.Name)
                .Append(" { get => Get<").Append(member.Type).Append(">(Slot(").Append(position).Append("),")
                .Append(Read(member.Identifier)).Append(',').Append(Write(member.Identifier))
                .Append("); set => Set(Slot(").Append(position).Append("),value,")
                .Append(Write(member.Identifier)).Append("); }\n");
        }

        _code.Append("}\n");
    }

    private void AppendFunctions(GeneratedType type)
    {
        _code.Append("private static ").Append(type.Type).Append(' ').Append(Read(type.Identifier))
            .Append("(object v, TypeTreeRepr s, IReader r) => ").Append(type.Case.Read).Append(";\n");
        _code.Append("private static object ").Append(Write(type.Identifier))
            .Append('(').Append(type.Type).Append(" v, TypeTreeRepr s, IReader r) => ")
            .Append(type.Case.Write).Append(";\n");
        _code.Append("private static bool ").Append(Match(type.Identifier))
            .Append("(TypeTreeRepr s) => ").Append(type.Case.Match).Append(";\n");
    }

    /// <summary>One name the dispatch table answers to.</summary>
    private sealed class DispatchArm
    {
        public readonly string InterfaceName;
        public readonly string Identifier;

        public DispatchArm(string interfaceName, string identifier)
        {
            InterfaceName = interfaceName;
            Identifier = identifier;
        }
    }

    /// <summary>One type the generated file mentions, with everything needed to write it out.</summary>
    private sealed class GeneratedType
    {
        public readonly string Type;
        public readonly string Identifier;
        public readonly AdapterCase Case;

        /// <summary>Whether the type gets an adapter class: it is one of the interfaces, or an instantiation of one.</summary>
        public readonly bool IsAdapter;

        public readonly IReadOnlyList<GeneratedMember> Members;

        public GeneratedType(string type, string identifier, AdapterCase adapterCase, bool isAdapter,
            IReadOnlyList<GeneratedMember> members)
        {
            Type = type;
            Identifier = identifier;
            Case = adapterCase;
            IsAdapter = isAdapter;
            Members = members;
        }
    }

    /// <summary>One property of an adapter class.</summary>
    private sealed class GeneratedMember
    {
        public readonly string Name;
        public readonly string Type;
        public readonly string Identifier;

        public GeneratedMember(string name, string type, string identifier)
        {
            Name = name;
            Type = type;
            Identifier = identifier;
        }
    }
}
