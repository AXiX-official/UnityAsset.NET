using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.Types.PreDefined;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types;

public class NodeObject : IUnityAsset
{
    protected readonly TypeTreeRepr Schema;
    protected readonly IReader Reader;
    
    private readonly object[] _members;
    private object?[]? _reads;
    private List<(int Index, Action Flush)>? _writeBacks;
    
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TypeTreeRepr, Dictionary<string, int>> Indexes = new();

    public string ClassName { get; internal set; }

    public NodeObject(object value, TypeTreeRepr schema, IReader reader)
    {
        _members = (object[])value; 
        Schema = schema; 
        Reader = reader; 
        ClassName = schema.TypeName;
    }
    
    protected T Get<T>(string name, Func<object, TypeTreeRepr, IReader, T> read,
        Func<T, TypeTreeRepr, IReader, object> write) =>
        Get(IndexOf(name), read, write);

    /// <summary>
    /// Reads the member in a resolved slot. The adapter resolves the slot once per schema, so this is the path a property
    /// getter takes; <paramref name="index"/> is -1 when the tree does not carry the member in this Unity version.
    /// </summary>
    protected T Get<T>(int index, Func<object, TypeTreeRepr, IReader, T> read,
        Func<T, TypeTreeRepr, IReader, object> write)
    {
        if (index < 0) return default!;

        if (_reads is { } cached && cached[index] is { } hit)
            return (T)hit;

        var schema = Schema.SubNodes[index];
        var raw = _members[index];
        var value = read(raw, schema, Reader);
        if (value is null) return default!;

        (_reads ??= new object?[Schema.SubNodes.Length])[index] = value;

        // A value that is the member itself, or a boxed one, cannot be mutated behind the tree's back, so nothing has to
        // be written back. Only a copy - a stand-in rebuilt while reading, a map flattened into pairs - does.
        if (!ReferenceEquals(value, raw) && !value.GetType().IsValueType)
            (_writeBacks ??= new()).Add((index, () => _members[index] = write(value, schema, Reader)));

        return value;
    }

    protected void Set<T>(string name, T value, Func<T, TypeTreeRepr, IReader, object> write) =>
        Set(IndexOf(name), value, write);

    protected void Set<T>(int index, T value, Func<T, TypeTreeRepr, IReader, object> write)
    {
        if (index < 0) return;
        var schema = Schema.SubNodes[index];
        _members[index] = write(value, schema, Reader);
        if (_reads is { } reads) reads[index] = value;
    }

    /// <summary>
    /// Resolves the slots an adapter's member names occupy in one schema: once per schema, not once per read. A name the
    /// tree does not carry resolves to -1, which the getter and setter treat as "this Unity version has no such member".
    /// </summary>
    protected static SlotMap MapOf(TypeTreeRepr schema, string[] names)
    {
        var slots = new int[names.Length];
        for (var index = 0; index < names.Length; index++)
            slots[index] = IndexOf(schema, names[index]);
        return new SlotMap(schema, slots);
    }

    /// <summary>
    /// A resolved member layout, kept in one reference so a reader can swap it without a schema and a slot array getting
    /// out of step: two threads parsing the same class must never read a slot resolved for the other one.
    /// </summary>
    protected sealed class SlotMap(TypeTreeRepr schema, int[] positions)
    {
        public readonly TypeTreeRepr Schema = schema;
        public readonly int[] Positions = positions;
    }

    /// <summary>The slot a member occupies, or -1 when the tree does not carry it in this Unity version.</summary>
    private int IndexOf(string name) => IndexOf(Schema, name);

    private static int IndexOf(TypeTreeRepr schema, string name)
    {
        // TryGetValue rather than ConditionalWeakTable.GetValue: the latter takes a callback delegate, and a read on the hot
        // path cannot afford one per call.
        if (!Indexes.TryGetValue(schema, out var table))
        {
            var built = BuildIndex(schema);
            try
            {
                Indexes.Add(schema, table = built);
            }
            catch (ArgumentException)
            {
                // Another thread won the race; its table is as good as this one.
                Indexes.TryGetValue(schema, out table);
            }
        }

        return table!.TryGetValue(name, out var index) ? index : -1;
    }

    private static Dictionary<string, int> BuildIndex(TypeTreeRepr schema)
    {
        var table = new Dictionary<string, int>(schema.SubNodes.Length * 2, StringComparer.Ordinal);

        // The stored name first, then the identifier form the generator emits, so an exact match always wins.
        for (var index = 0; index < schema.SubNodes.Length; index++)
            table[schema.SubNodes[index].Name] = index;
        for (var index = 0; index < schema.SubNodes.Length; index++)
            table.TryAdd(Identifier(schema.SubNodes[index].Name), index);

        return table;
    }
    
    // Match UnityTypeGen's identifier normalization; @ is a C# escape, not part of the stored name.
    private static string Identifier(string name) => (name.Length > 0 && char.IsDigit(name[0]) ? "_" : "")
        + new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

    internal object Flush()
    {
        if (_writeBacks is { } writeBacks)
            foreach (var (_, flush) in writeBacks) flush();
        return _members;
    }
    
    public void Write(IWriter writer) => NodeData.WriteValue(writer, Schema, Flush());
    
    public string ToPlainText()
    {
        var builder = new StringBuilder();
        ToAssetNode().ToPlainText(builder);
        return builder.ToString();
    }
    
    public AssetNode ToAssetNode(string name = "Base") => ToNode(Flush(), Schema, name);
    
    private static AssetNode ToNode(object value, TypeTreeRepr schema, string name)
    {
        var result = new AssetNode
        {
            Name = name, 
            TypeName = schema.TypeName
        };
        var subNodes = schema.SubNodes;
        // A value the tree keeps as itself - a pointer, a hand-written stand-in - describes its own shape: it knows the
        // members a pointer is, which the tree no longer spells out.
        if (value is IUnityObject concrete && concrete.ToAssetNode(name) is { } described)
            return described;

        if (schema.Kind == NodeKind.Class && value is object[] members)
        {
            for (var index = 0; index < subNodes.Length; index++)
                result.Children.Add(ToNode(members[index], subNodes[index], subNodes[index].Name));
        }
        else if (schema.Kind == NodeKind.Array && value is Array items)
        {
            var element = Element(schema);
            foreach (var item in items) result.Children.Add(ToNode(item, element, element.Name));
        }
        else
        {
            result.Value = value;
        }
        return result;
    }
    
    internal static TypeTreeRepr Element(TypeTreeRepr s) => s.SubNodes[0].SubNodes[1];
    
    /// <summary>
    /// The elements of a vector as the adapter layer sees them: the values themselves for a plain vector, and the map's
    /// pairs - which are what a `<c>pair</c>` adapter reads - for a map.
    /// </summary>
    internal static object[] Elements(object v) 
        => v is object[] values
            ? values
            : ((List<KeyValuePair<object, object>>)v).Select(x => (object)x).ToArray();
    
    /// <summary>A vector of plain values is kept as one typed array, so reading it is the cast the node tree promised.</summary>
    internal static T[] LeafRun<T>(object value) => (T[])value;

    /// <summary>
    /// The pointer the tree holds. A pointer is its two scalars, so an adapter can build the requested
    /// <c>PPtr&lt;T&gt;</c> from it without encoding anything.
    /// </summary>
    internal static PPtrRef Pointer(object value) => (PPtrRef)value;

    /// <summary>The same, from a typed pointer being written back: the tree keeps only what the wire carries.</summary>
    internal static PPtrRef Pointer(IPPtr pointer) => new(pointer.m_FileID, pointer.m_PathID);

    /// <summary>A vector of structures as one typed array, whether the tree keeps a flat run or one node per element.</summary>
    internal static T[] StructArray<T>(object value, TypeTreeRepr element) where T : unmanaged
        => NodeData.StructArray<T>(value, element);

    /// <summary>The same vector back as the tree holds it.</summary>
    internal static object StructValues<T>(T[] values, TypeTreeRepr element) where T : unmanaged
        => NodeData.StructValues<T>(values, element);
    
    internal static object ArrayValue(object[] values, TypeTreeRepr s) 
        => s.Kind == NodeKind.Map
            ? values.Select(x => (object[])x).Select(x => new KeyValuePair<object, object>(x[0], x[1])).ToList()
            : values;
    
    internal static object PairItem(object v, string name) 
        => v is KeyValuePair<object, object> p
            ? (name == "first" ? p.Key : p.Value) 
            : ((object[])v)[name == "first" ? 0 : 1];
    
    /// <summary>A map entry, or the two slots of a `<c>pair</c>` node: both are one member per side.</summary>
    internal static object Pair(object a, object b, TypeTreeRepr s) => new object[] { a, b };

    /// <summary>
    /// Turns a typed value back into the node tree the schema describes, so that it can be written by
    /// <see cref="NodeData"/>. A value that needs to know the asset file it belongs to - the hand-written stand-ins
    /// whose layout depends on the Unity version, such as StreamingInfo - is encoded with an <see cref="AssetWriter"/>;
    /// without one, those types refuse to write.
    /// </summary>
    internal static object WriteConcrete(IUnityObject value, TypeTreeRepr schema, IReader reader)
    {
        if (value is NodeObject node) 
            return node.Flush();
        
        using var stream = new MemoryStream();
        // A stand-in is small, and one 8 KB buffer per written member is a lot of bytes for a 12-byte pointer. AssetWriter
        // keeps the asset context the version-dependent layouts need.
        IDisposable sink = (reader as AssetReader)?.AssetsFile is { } assetsFile
            ? new AssetWriter(stream, assetsFile, reader.Endian, leaveOpen: true, bufferSize: 64)
            : new CustomStreamWriter(stream, reader.Endian, leaveOpen: true, bufferSize: 64);
        using (sink)
            value.Write((IWriter)sink);
        return NodeData.ReadValue(new MemoryReader(stream.ToArray(), endian: reader.Endian), schema);
    }
}

/// <summary>Root object of a class the generated adapters do not cover: its tree has no interface, but it carries a name.</summary>
internal sealed class NamedNodeObject(object value, TypeTreeRepr schema, IReader reader)
    : NodeObject(value, schema, reader), INamedObject
{
    public string m_Name
    {
        get => Get("m_Name", (v, _, _) => (string)v, (v, _, _) => v);
        set => Set("m_Name", value, (v, _, _) => v);
    }
}