using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.Types.PreDefined;
namespace UnityAsset.NET.Types;
/// <summary>Schema-backed object with typed mutable values flushed before serialization.</summary>
public class NodeObject : IUnityAsset
{
    protected readonly TypeTreeRepr Schema;
    protected readonly IReader Reader;
    private readonly Dictionary<string, NodeData> _members;
    private readonly Dictionary<string, (object? Value, Action Flush)> _cache = new();
    public string ClassName { get; internal set; }
    public NodeObject(object value, TypeTreeRepr schema, IReader reader)
    { _members = (Dictionary<string, NodeData>)value; Schema = schema; Reader = reader; ClassName = schema.TypeName; }
    protected T Get<T>(string name, Func<object, TypeTreeRepr, IReader, T> read, Func<T, TypeTreeRepr, object> write)
    {
        if (_cache.TryGetValue(name, out var cached)) return (T)cached.Value!;
        var schema = FindMember(name);
        if (schema is null) return default!;
        var node = _members[schema.Name];
        var value = read(node.Value, schema, Reader);
        _cache[name] = (value, () => node.Value = write(value, schema));
        return value;
    }
    protected void Set<T>(string name, T value, Func<T, TypeTreeRepr, object> write)
    {
        var schema = FindMember(name);
        if (schema is null) return;
        var node = _members[schema.Name];
        node.Value = write(value, schema);
        _cache[name] = (value, () => node.Value = write(value, schema));
    }
    private TypeTreeRepr? FindMember(string name) => Schema.SubNodes.FirstOrDefault(x => x.Name == name)
        ?? Schema.SubNodes.FirstOrDefault(x => Identifier(x.Name) == name);
    // Match UnityTypeGen's identifier normalization; @ is a C# escape, not part of the stored name.
    private static string Identifier(string name) => (name.Length > 0 && char.IsDigit(name[0]) ? "_" : "")
        + new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    internal object Flush() { foreach (var entry in _cache.Values) entry.Flush(); return _members; }
    public void Write(IWriter writer) => NodeData.WriteValue(writer, Schema, Flush());
    public string ToPlainText() => NodeData.ObjectToString(Flush());
    public AssetNode ToAssetNode(string name = "Base") => ToNode(Flush(), Schema, name);
    private static AssetNode ToNode(object value, TypeTreeRepr schema, string name)
    {
        var result = new AssetNode { Name = name, TypeName = schema.TypeName };
        if (value is Dictionary<string, NodeData> members)
            foreach (var child in schema.SubNodes) result.Children.Add(ToNode(members[child.Name].Value, child, child.Name));
        else if (value is NodeData[] items)
            foreach (var item in items) result.Children.Add(ToNode(item.Value, Element(schema), item.Name));
        else result.Value = value;
        return result;
    }
    internal static TypeTreeRepr Element(TypeTreeRepr s) => s.SubNodes[0].SubNodes[1];
    internal static object[] Elements(object v) => v is NodeData[] a ? a.Select(x => x.Value).ToArray()
        : ((List<KeyValuePair<object, object>>)v).Select(x => (object)x).ToArray();
    internal static object ArrayValue(object[] values, TypeTreeRepr s) => s.TypeName == "map"
        ? values.Select(x => (Dictionary<string, NodeData>)x).Select(x => new KeyValuePair<object, object>(x["first"].Value, x["second"].Value)).ToList()
        : values.Select(x => new NodeData(Element(s).TypeName, Element(s).Name, x)).ToArray();
    internal static object PairItem(object v, string name) => v is KeyValuePair<object, object> p
        ? (name == "first" ? p.Key : p.Value) : ((Dictionary<string, NodeData>)v)[name].Value;
    internal static object Pair(object a, object b, TypeTreeRepr s) => new Dictionary<string, NodeData>
    { ["first"] = new(s.SubNodes[0].TypeName, "first", a), ["second"] = new(s.SubNodes[1].TypeName, "second", b) };
    internal static T ReadConcrete<T>(object value, TypeTreeRepr schema, IReader reader, Func<IReader, T> create)
    {
        using var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, reader.Endian, leaveOpen: true)) NodeData.WriteValue(writer, schema, value);
        var bytes = stream.ToArray();
        IReader source = new MemoryReader(bytes, endian: reader.Endian);
        if (reader is AssetReader asset) source = new AssetReader(new BufferProvider(bytes), 0, (ulong)bytes.Length, asset.AssetsFile, reader.Endian);
        return create(source);
    }
    internal static object WriteConcrete(IUnityObject value, TypeTreeRepr schema)
    {
        if (value is NodeObject node) return node.Flush();
        using var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true)) value.Write(writer);
        return NodeData.ReadValue(new MemoryReader(stream.ToArray()), schema);
    }
    private sealed class BufferProvider(byte[] bytes) : IReaderProvider
    { public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new MemoryReader(bytes, endian: endian); }
}
internal sealed class NamedNodeObject(object value, TypeTreeRepr schema, IReader reader) : NodeObject(value, schema, reader), INamedObject
{
    public string m_Name { get => Get("m_Name", (v, _, _) => (string)v, (v, _) => v); set => Set("m_Name", value, (v, _) => v); }
}
