using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static UnityAsset.NET.SourceGenerator.NodeAdapter.GeneratedNames;

namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>
/// One type's three generated functions. They are produced together, by one case, so the correspondence between how a
/// shape is read, written and recognised is a property of the code that returns them rather than something a reader has
/// to reconstruct.
/// </summary>
internal readonly struct AdapterCase
{
    public readonly string Read;
    public readonly string Write;
    public readonly string Match;

    // The three bodies are all strings, so the parameters are spelled the same as the members: a call site names which
    // body it is passing, and swapping two of them cannot go unnoticed.
    public AdapterCase(string Read, string Write, string Match)
    {
        this.Read = Read;
        this.Write = Write;
        this.Match = Match;
    }
}

/// <summary>
/// Decides what each type the interfaces mention looks like to the emitter: nullable, array, variant, pair, one of the
/// generated interfaces, a primitive, or a hand-written stand-in. Each case is one small method that returns all three
/// bodies, so a shape can be read - and changed - in one place.
/// </summary>
internal sealed class NodeAdapterShape
{
    private readonly InterfaceModel _model;

    /// <summary>Gives the emitted identifier for a type, registering it when the emitter has not seen it yet.</summary>
    private readonly Func<string, string> _name;

    public NodeAdapterShape(InterfaceModel model, Func<string, string> name)
    {
        _model = model;
        _name = name;
    }

    public AdapterCase Case(string type)
    {
        var syntax = SyntaxFactory.ParseTypeName(type);
        return syntax switch
        {
            NullableTypeSyntax nullable => Nullable(nullable),
            ArrayTypeSyntax array => Array(array),
            GenericNameSyntax { Identifier.Text: "RefSum" } union => Variant(union, type),
            GenericNameSyntax { Identifier.Text: "ValueTuple" } pair => Pair(pair, type),
            _ => Named(syntax, type),
        };
    }

    private AdapterCase Nullable(NullableTypeSyntax nullable)
    {
        var inner = nullable.ElementType.ToString();
        var name = _name(inner);
        return new AdapterCase(
            Read: $"{Read(name)}(v,s,r)",
            Write: $"{Write(name)}(({inner})v!,s,r)",
            Match: $"{Match(name)}(s)");
    }

    private AdapterCase Array(ArrayTypeSyntax array)
    {
        // Dropping one rank specifier is what makes the element of a jagged array the array one rank lower.
        var element = array.RankSpecifiers.Count == 1
            ? array.ElementType.ToString()
            : array.WithRankSpecifiers(array.RankSpecifiers.RemoveAt(0)).ToString();
        var name = _name(element);
        var match =
            $"s.SubNodes.Length == 1 && s.SubNodes[0].TypeName == \"Array\" && {Match(name)}(NodeObject.Element(s))";

        // A vector of plain values is one typed array in the node tree, so reading it is a cast and writing it hands
        // that array back. Elements that are structures stay one node each, which is what the element adapter reads.
        if (UnityTypeNames.IsRunElement(element))
            return new AdapterCase(Read: $"NodeObject.LeafRun<{element}>(v)", Write: "v", Match: match);

        // A vector of structures the catalog knows is kept as one flat run of the scalars those structures are: the two
        // sides reinterpret the same bytes, so neither direction builds a node per element.
        if (UnityTypeNames.ConcreteStructs.Contains(element))
            return new AdapterCase(
                Read: $"NodeObject.StructRun<{element}>(v, NodeObject.Element(s))",
                Write: $"NodeObject.StructValues(v, NodeObject.Element(s))",
                Match: match);

        return new AdapterCase(
            Read: $"NodeObject.Elements(v).Select(x => {Read(name)}(x,NodeObject.Element(s),r)).ToArray()",
            Write: $"NodeObject.ArrayValue(v.Select(x => {Write(name)}(x,NodeObject.Element(s),r)).ToArray(),s)",
            Match: match);
    }

    /// <summary>
    /// A member whose type the Unity versions disagree about. The schema carries the alternative, so reading picks the
    /// first one the tree matches and writing picks the one the value's own type matches.
    /// </summary>
    private AdapterCase Variant(GenericNameSyntax union, string type)
    {
        var alternatives = union.TypeArgumentList.Arguments.Select(argument => argument.ToString()).ToArray();
        var names = alternatives.Select(_name).ToArray();

        // The generic pointer matches every PPtr, so it is tried last: first otherwise it would shadow the pointer to
        // the specific target the tree actually means.
        var readOrder = names
            .Select((name, index) => (Name: name, Index: index))
            .OrderBy(candidate => alternatives[candidate.Index].Contains("PPtr<IUnityObject>") ? 1 : 0);

        var read = string.Concat(readOrder.Select(candidate =>
                $"{Match(candidate.Name)}(s) ? new {type}({candidate.Index},{Read(candidate.Name)}(v,s,r)) : "))
            + "throw new NotSupportedException($\"No variant matches {s.TypeName}.\")";

        var write = string.Concat(names.Select((name, index) =>
                $"v.Value is {alternatives[index]} a{index} ? {Write(name)}(a{index},s,r) : "))
            + "throw new NotSupportedException(\"Invalid variant.\")";

        return new AdapterCase(read, write, string.Join(" || ", names.Select(name => $"{Match(name)}(s)")));
    }

    /// <summary>A map entry: the tree's <c>pair</c> node, read and written key first.</summary>
    private AdapterCase Pair(GenericNameSyntax pair, string type)
    {
        var first = pair.TypeArgumentList.Arguments[0].ToString();
        var second = pair.TypeArgumentList.Arguments[1].ToString();
        var a = _name(first);
        var b = _name(second);
        return new AdapterCase(
            Read: $"new {type}({Read(a)}(NodeObject.PairItem(v,\"first\"),s.SubNodes[0],r),{Read(b)}(NodeObject.PairItem(v,\"second\"),s.SubNodes[1],r))",
            Write: $"NodeObject.Pair({Write(a)}(v.Item1,s.SubNodes[0],r),{Write(b)}(v.Item2,s.SubNodes[1],r),s)",
            Match: "s.TypeName == \"pair\"");
    }

    /// <summary>
    /// Everything named: one of the generated interfaces, a primitive the tree decodes directly, or a hand-written
    /// stand-in that encodes itself through a byte round trip.
    /// </summary>
    private AdapterCase Named(TypeSyntax syntax, string type)
    {
        var name = syntax is GenericNameSyntax generic ? generic.Identifier.Text : type;

        if (_model.IsInterface(type))
            return new AdapterCase(
                Read: $"new {Adapter(_name(type))}(v,s,r)",
                Write: "NodeObject.WriteConcrete(v,s,r)",
                Match: UnityTypeNames.Exact(_model.SchemaName(type)));

        // A pointer is two scalars on the wire and two scalars in the tree, so the target type is the only thing missing:
        // the adapter supplies it, and neither direction needs to encode the value into bytes first.
        if (name == "PPtr")
            return new AdapterCase(
                Read: $"new {type}(NodeObject.Pointer(v), r)",
                Write: "NodeObject.Pointer(v)",
                Match: SchemaMatch(syntax, type, name));

        var decodesItself = syntax is PredefinedTypeSyntax || name == "TypelessData" || UnityTypeNames.Concrete.Contains(type);
        var read = decodesItself ? $"({type})v" : $"NodeObject.ReadConcrete(v,s,r,x => new {type}(x))";
        var write = decodesItself ? "v" : "NodeObject.WriteConcrete(v,s,r)";
        var match = SchemaMatch(syntax, type, name);

        // Interface schemas unify the two signednesses of one width, so this rewrite applies exactly to the primitives
        // that are one of these integers - and it is written where that branch is, not applied afterwards.
        if (!decodesItself || !UnityTypeNames.Signedness.TryGetValue(type, out var pair))
            return new AdapterCase(read, write, match);

        var other = UnityTypeNames.Or(pair.UnityNames);
        return new AdapterCase(
            Read: $"v is {pair.Other} n ? unchecked(({type})n) : ({type})v",
            Write: $"({other}) ? (object)unchecked(({pair.Other})v) : v",
            Match: $"({match}) || ({other})");
    }

    /// <summary>The test the variant reader uses to tell one schema shape from another.</summary>
    private string SchemaMatch(TypeSyntax syntax, string type, string name)
    {
        // A pointer to IUnityObject is what an unknown target falls back to, so it has to match any pointer.
        if (type == "PPtr<IUnityObject>") return UnityTypeNames.AnyPointer;

        var unityName = UnityName(syntax, type, name);

        // A specific pointer is matched by the name that spells its target.
        if (name == "PPtr") return UnityTypeNames.Exact(unityName);

        return UnityTypeNames.Matches(name, unityName);
    }

    /// <summary>
    /// The Unity type name a C# type is spelled as. An interface is spelled as the class it adapts, and a pointer is
    /// spelled after its target.
    /// </summary>
    private string UnityName(TypeSyntax syntax, string type, string name)
    {
        if (name == "PPtr" && syntax is GenericNameSyntax pointer)
        {
            var target = pointer.TypeArgumentList.Arguments[0].ToString();
            return "PPtr<" + (target == "IUnityObject" ? "Object" : target.TrimStart('I')) + ">";
        }

        if (name.StartsWith("I", StringComparison.Ordinal) && _model.IsInterface(type)) return name.Substring(1);

        return type.Replace(" ", "");
    }
}
