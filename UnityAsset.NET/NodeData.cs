using System.Runtime.InteropServices;
using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined;
using UnityAsset.NET.Types.PreDefined.Types;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class NodeData
{
    public string Type;
    public string Name;
    public object Value;
    
    public T As<T>()
    {
        return (T)Value;
    }
    
    public NodeData(IReader reader, TypeTreeRepr current)
    {
        Type = current.TypeName;
        Name = current.Name;
        Value = ReadValue(reader, current);
    }
    
    public static object ReadValue(IReader reader, TypeTreeRepr current)
    {
        object value;
        var align = current.RequiresAlign;
        switch (current.Kind)
        {
            case NodeKind.SInt8:
                value = reader.ReadInt8();
                break;
            case NodeKind.UInt8:
                value = reader.ReadUInt8();
                break;
            case NodeKind.Char:
                value = BitConverter.ToChar(reader.ReadSpan(2));
                break;
            case NodeKind.Int16:
                value = reader.ReadInt16();
                break;
            case NodeKind.UInt16:
                value = reader.ReadUInt16();
                break;
            case NodeKind.Int32:
                value = reader.ReadInt32();
                break;
            case NodeKind.UInt32:
                value = reader.ReadUInt32();
                break;
            case NodeKind.Int64:
                value = reader.ReadInt64();
                break;
            case NodeKind.UInt64:
                value = reader.ReadUInt64();
                break;
            case NodeKind.Single:
                value = reader.ReadSingle();
                break;
            case NodeKind.Double:
                value = reader.ReadDouble();
                break;
            case NodeKind.Boolean:
                value = reader.ReadBoolean();
                break;
            case NodeKind.String:
                value = reader.ReadSizedString();
                break;
            case NodeKind.Map:
            {
                var pair = current.SubNodes[0].SubNodes[1];
                align |= pair.RequiresAlign;
                var first = pair.SubNodes[0];
                var second = pair.SubNodes[1];
                var size = reader.ReadInt32();
                var dic = new List<KeyValuePair<object, object>>(size);
                for (int j = 0; j < size; j++)
                {
                    dic.Add(new KeyValuePair<object, object>(ReadValue(reader, first), ReadValue(reader, second)));
                    if (pair.RequiresAlign) reader.Align(4);
                }
                value = dic;
                break;
                }
            case NodeKind.TypelessData:
                {
                    value = new TypelessData(reader);
                    break;
                }
            case NodeKind.Array:
                {
                    var vector = current.SubNodes[0];
                    align |= vector.RequiresAlign;
                    var size = reader.ReadInt32();
                    var element = vector.SubNodes[1];


                    if (IsCsharpPrim(element.Kind)) value = ReadCsharpPrim(reader, element, size);
                    else if (IsPrimArray(element) is { } flat) value = ReadPrimArray(reader, element, size, flat);
                    else if (size == 0) value = Array.Empty<object>();
                    else
                    {
                        var items = new object[size];
                        for (int j = 0; j < size; j++)
                        {
                            items[j] = ReadValue(reader, element);
                        }
                        value = items;
                    }
                    break;
                }
            default: // Class
                {
                    if (current.TypeName.StartsWith("PPtr<", StringComparison.Ordinal))
                    {
                        value = new PPtrRef(reader);
                        break;
                    }
                    
                    if (PreDefinedTypeTable.TryCreate(current.TypeName, reader, out var concrete))
                    {
                        value = concrete;
                        break;
                    }

                    var subNodes = current.SubNodes;
                    var members = new object[subNodes.Length];
                    for (int j = 0; j < subNodes.Length; j++)
                    {
                        members[j] = ReadValue(reader, subNodes[j]);
                    }
                    value = members;
                    break;
                }
        }
        if (align)
            reader.Align(4);
        return value;
    }
    
    private static bool IsCsharpPrim(NodeKind kind) => kind is NodeKind.SInt8 or NodeKind.UInt8 or NodeKind.Int16
        or NodeKind.UInt16 or NodeKind.Int32 or NodeKind.UInt32 or NodeKind.Int64 or NodeKind.UInt64
        or NodeKind.Single or NodeKind.Double or NodeKind.String;
    
    private static (NodeKind Kind, int Leaves)? IsPrimArray(TypeTreeRepr element)
    {
        if (element.Kind != NodeKind.Class || element.RequiresAlign || element.SubNodes.Length == 0)
            return null;

        if (!PreDefinedTypeTable.ByExactName.TryGetValue(element.TypeName, out var predefined) || !predefined.Type.IsValueType)
            return null;

        NodeKind? kind = null;
        var leaves = 0;
        foreach (var member in element.SubNodes)
        {
            if (!IsCsharpPrim(member.Kind) || member.Kind == NodeKind.String || member.RequiresAlign || member.SubNodes.Length != 0)
                return null;
            if (kind is null) kind = member.Kind;
            else if (kind != member.Kind) return null;
            leaves++;
        }

        return kind is null ? null : (kind.Value, leaves);
    }

    /// <summary>
    /// The vector as one typed array. A flat run in the tree is the same bytes an element struct is, so this is a
    /// reinterpretation rather than a conversion; a node per element is unpacked into the same array.
    /// </summary>
    internal static T[] StructRun<T>(object value, TypeTreeRepr element) where T : unmanaged
    {
        if (value is object[] nodes)
        {
            var items = new T[nodes.Length];
            for (var index = 0; index < items.Length; index++) items[index] = (T)nodes[index];
            return items;
        }

        if (IsPrimArray(element) is not { } flat)
            throw new InvalidOperationException($"{element.TypeName} is not stored as a flat run");

        return flat.Kind switch
        {
            NodeKind.SInt8 => Cast<sbyte, T>((sbyte[])value),
            NodeKind.UInt8 => Cast<byte, T>((byte[])value),
            NodeKind.Int16 => Cast<short, T>((short[])value),
            NodeKind.UInt16 => Cast<ushort, T>((ushort[])value),
            NodeKind.Int32 => Cast<int, T>((int[])value),
            NodeKind.UInt32 => Cast<uint, T>((uint[])value),
            NodeKind.Int64 => Cast<long, T>((long[])value),
            NodeKind.UInt64 => Cast<ulong, T>((ulong[])value),
            NodeKind.Single => Cast<float, T>((float[])value),
            NodeKind.Double => Cast<double, T>((double[])value),
            _ => throw new NotSupportedException($"A {flat.Kind} run is not a scalar"),
        };
    }

    /// <summary>The same vector back as the tree holds it: a flat scalar run, or one node per element.</summary>
    internal static object StructValues<T>(T[] values, TypeTreeRepr element) where T : unmanaged
    {
        if (IsPrimArray(element) is not { } flat)
        {
            var items = new object[values.Length];
            for (var index = 0; index < items.Length; index++) items[index] = values[index];
            return items;
        }

        return flat.Kind switch
        {
            NodeKind.SInt8 => Cast<T, sbyte>(values),
            NodeKind.UInt8 => Cast<T, byte>(values),
            NodeKind.Int16 => Cast<T, short>(values),
            NodeKind.UInt16 => Cast<T, ushort>(values),
            NodeKind.Int32 => Cast<T, int>(values),
            NodeKind.UInt32 => Cast<T, uint>(values),
            NodeKind.Int64 => Cast<T, long>(values),
            NodeKind.UInt64 => Cast<T, ulong>(values),
            NodeKind.Single => Cast<T, float>(values),
            NodeKind.Double => Cast<T, double>(values),
            _ => throw new NotSupportedException($"A {flat.Kind} run is not a scalar"),
        };
    }

    /// <summary>
    /// Reinterprets a run as the other type. The two have to be the same shape - an element struct and its scalars - so a
    /// mismatch throws instead of handing out a vector of the wrong length.
    /// </summary>
    private static TTarget[] Cast<TSource, TTarget>(TSource[] source)
        where TSource : unmanaged
        where TTarget : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(source.AsSpan());
        var size = System.Runtime.CompilerServices.Unsafe.SizeOf<TTarget>();
        if (bytes.Length % size != 0)
            throw new InvalidOperationException(
                $"A run of {bytes.Length} bytes is not a whole number of {typeof(TTarget).Name} ({size} bytes)");

        return MemoryMarshal.Cast<TSource, TTarget>(source.AsSpan()).ToArray();
    }

    /// <summary>Reads a vector of same-kinded structures as one run of its scalars.</summary>
    private static object ReadPrimArray(IReader reader, TypeTreeRepr element, int count, (NodeKind Kind, int Leaves) flat) =>
        flat.Kind switch
        {
            NodeKind.SInt8 => ReadCsharpPrim<sbyte>(reader, element, count * flat.Leaves, 1),
            NodeKind.UInt8 => ReadCsharpPrim<byte>(reader, element, count * flat.Leaves, 1),
            NodeKind.Int16 => ReadCsharpPrim<short>(reader, element, count * flat.Leaves, 2),
            NodeKind.UInt16 => ReadCsharpPrim<ushort>(reader, element, count * flat.Leaves, 2),
            NodeKind.Int32 => ReadCsharpPrim<int>(reader, element, count * flat.Leaves, 4),
            NodeKind.UInt32 => ReadCsharpPrim<uint>(reader, element, count * flat.Leaves, 4),
            NodeKind.Int64 => ReadCsharpPrim<long>(reader, element, count * flat.Leaves, 8),
            NodeKind.UInt64 => ReadCsharpPrim<ulong>(reader, element, count * flat.Leaves, 8),
            NodeKind.Single => ReadCsharpPrim<float>(reader, element, count * flat.Leaves, 4),
            NodeKind.Double => ReadCsharpPrim<double>(reader, element, count * flat.Leaves, 8),
            _ => throw new NotSupportedException($"A {flat.Kind} run is not a scalar"),
        };

    private static object ReadCsharpPrim(IReader reader, TypeTreeRepr element, int count) => element.Kind switch
    {
        NodeKind.SInt8 => ReadCsharpPrim<sbyte>(reader, element, count, 1),
        NodeKind.UInt8 => ReadCsharpPrim<byte>(reader, element, count, 1),
        NodeKind.Int16 => ReadCsharpPrim<short>(reader, element, count, 2),
        NodeKind.UInt16 => ReadCsharpPrim<ushort>(reader, element, count, 2),
        NodeKind.Int32 => ReadCsharpPrim<int>(reader, element, count, 4),
        NodeKind.UInt32 => ReadCsharpPrim<uint>(reader, element, count, 4),
        NodeKind.Int64 => ReadCsharpPrim<long>(reader, element, count, 8),
        NodeKind.UInt64 => ReadCsharpPrim<ulong>(reader, element, count, 8),
        NodeKind.Single => ReadCsharpPrim<float>(reader, element, count, 4),
        NodeKind.Double => ReadCsharpPrim<double>(reader, element, count, 8),
        NodeKind.String => ReadStringRun(reader, element, count),
        _ => Array.Empty<NodeData>(),
    };

    private static T[] ReadCsharpPrim<T>(IReader reader, TypeTreeRepr element, int count, int elementSize)
        where T : unmanaged
    {
        if (count == 0) return Array.Empty<T>();

        // An element that is padded on its own cannot be copied in bulk: read them one at a time, as the node path did.
        if (element.RequiresAlign)
        {
            var aligned = new T[count];
            for (var index = 0; index < count; index++) aligned[index] = (T)ReadValue(reader, element);
            return aligned;
        }

        var values = MemoryMarshal.Cast<byte, T>(reader.ReadSpan(count * elementSize)).ToArray();
        if (NeedsSwap(reader.Endian)) SwapEndianness(values.AsSpan(), elementSize);
        return values;
    }

    private static string[] ReadStringRun(IReader reader, TypeTreeRepr element, int count)
    {
        if (count == 0) return Array.Empty<string>();

        var values = new string[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = reader.ReadSizedString();
            if (element.RequiresAlign) reader.Align(4);
        }

        return values;
    }

    private static void WriteCsharpPrim<T>(IWriter writer, T[] values, int elementSize, bool align, Action<IWriter, T> write)
        where T : unmanaged
    {
        writer.WriteInt32(values.Length);
        WriteRunValues(writer, values, elementSize, align, write);
    }

    private static void WriteRunValues<T>(IWriter writer, T[] values, int elementSize, bool align, Action<IWriter, T> write)
        where T : unmanaged
    {
        // One copy when the bytes are already in the file's order and nothing is padded between them.
        if (!align && !NeedsSwap(writer.Endian))
        {
            writer.WriteBytes(MemoryMarshal.AsBytes(values.AsSpan()));
            return;
        }

        foreach (var value in values)
        {
            write(writer, value);
            if (align) writer.Align(4);
        }
    }

    private static bool NeedsSwap(Endianness endian) =>
        (endian == Endianness.BigEndian) != !BitConverter.IsLittleEndian;

    private static void SwapEndianness<T>(Span<T> values, int elementSize) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(values);
        for (var offset = 0; offset < bytes.Length; offset += elementSize)
            for (int left = offset, right = offset + elementSize - 1; left < right; left++, right--)
                (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
    }
    
    public static void WriteValue(IWriter writer, TypeTreeRepr current, object value)
    {
        var align = current.RequiresAlign;
        switch (current.Kind)
        {
            case NodeKind.SInt8:
                writer.WriteInt8((sbyte)value);
                break;
            case NodeKind.UInt8:
                writer.WriteUInt8((byte)value);
                break;
            case NodeKind.Char:
                writer.WriteChar((char)value);
                break;
            case NodeKind.Int16:
                writer.WriteInt16((short)value);
                break;
            case NodeKind.UInt16:
                writer.WriteUInt16((ushort)value);
                break;
            case NodeKind.Int32:
                writer.WriteInt32((int)value);
                break;
            case NodeKind.UInt32:
                writer.WriteUInt32((uint)value);
                break;
            case NodeKind.Int64:
                writer.WriteInt64((long)value);
                break;
            case NodeKind.UInt64:
                writer.WriteUInt64((ulong)value);
                break;
            case NodeKind.Single:
                writer.WriteSingle((float)value);
                break;
            case NodeKind.Double:
                writer.WriteDouble((double)value);
                break;
            case NodeKind.Boolean:
                writer.WriteBoolean((bool)value);
                break;
            case NodeKind.String:
                writer.WriteSizedString((string)value);
                break;
            case NodeKind.Map:
                {
                    var pair = current.SubNodes[0].SubNodes[1];
                    align |= pair.RequiresAlign;
                    var first = pair.SubNodes[0];
                    var second = pair.SubNodes[1];
                    var pairs = (List<KeyValuePair<object, object>>)value;
                    writer.WriteInt32(pairs.Count);
                    foreach (var entry in pairs)
                    {
                        WriteValue(writer, first, entry.Key);
                        WriteValue(writer, second, entry.Value);
                        if (pair.RequiresAlign) writer.Align(4);
                    }
                    break;
                }
            case NodeKind.TypelessData:
                ((TypelessData)value).Write(writer);
                break;
            case NodeKind.Array:
                {
                    var vector = current.SubNodes[0];
                    align |= vector.RequiresAlign;
                    var element = vector.SubNodes[1];

                    // The schema says which encoding the tree holds: a run of plain values, or one value per element.
                    if (IsCsharpPrim(element.Kind))
                    {
                        WriteCsharpPrim(writer, element, value);
                        break;
                    }

                    // A vector of same-kinded structures is one flat run: its length is the scalar count, not the
                    // element count.
                    if (IsPrimArray(element) is { } flat)
                    {
                        WritePrimArray(writer, element, value, flat);
                        break;
                    }

                    var items = (object[])value;
                    writer.WriteInt32(items.Length);
                    foreach (var item in items) WriteValue(writer, element, item);
                    break;
                }
            default: // Class: one member per subnode, in the same order they were read
                {
                    // The tree's concrete values encode themselves: a type the catalog knows writes through its own
                    // writer, and a pointer is its two scalars.
                    if (PreDefinedTypeTable.TryWrite(current.TypeName, writer, value))
                        break;

                    if (value is IUnityObject concrete)
                    {
                        concrete.Write(writer);
                        break;
                    }

                    var subNodes = current.SubNodes;
                    var members = (object[])value;
                    for (var index = 0; index < subNodes.Length; index++)
                    {
                        WriteValue(writer, subNodes[index], members[index]);
                    }
                    break;
                }
        }
        if (align)
            writer.Align(4);
    }

    private static void WriteCsharpPrim(IWriter writer, TypeTreeRepr element, object value)
    {
        var align = element.RequiresAlign;
        switch (element.Kind)
        {
            case NodeKind.SInt8: WriteCsharpPrim(writer, (sbyte[])value, 1, align, static (w, v) => w.WriteInt8(v)); break;
            case NodeKind.UInt8: WriteCsharpPrim(writer, (byte[])value, 1, align, static (w, v) => w.WriteUInt8(v)); break;
            case NodeKind.Int16: WriteCsharpPrim(writer, (short[])value, 2, align, static (w, v) => w.WriteInt16(v)); break;
            case NodeKind.UInt16: WriteCsharpPrim(writer, (ushort[])value, 2, align, static (w, v) => w.WriteUInt16(v)); break;
            case NodeKind.Int32: WriteCsharpPrim(writer, (int[])value, 4, align, static (w, v) => w.WriteInt32(v)); break;
            case NodeKind.UInt32: WriteCsharpPrim(writer, (uint[])value, 4, align, static (w, v) => w.WriteUInt32(v)); break;
            case NodeKind.Int64: WriteCsharpPrim(writer, (long[])value, 8, align, static (w, v) => w.WriteInt64(v)); break;
            case NodeKind.UInt64: WriteCsharpPrim(writer, (ulong[])value, 8, align, static (w, v) => w.WriteUInt64(v)); break;
            case NodeKind.Single: WriteCsharpPrim(writer, (float[])value, 4, align, static (w, v) => w.WriteSingle(v)); break;
            case NodeKind.Double: WriteCsharpPrim(writer, (double[])value, 8, align, static (w, v) => w.WriteDouble(v)); break;
            case NodeKind.String: WriteStringRun(writer, (string[])value, align); break;
            default:
                throw new NotSupportedException($"A {element.Kind} vector is not written as a run.");
        }
    }

    /// <summary>
    /// Writes a vector of same-kinded structures back: the count is the element count, while the run holds one value per
    /// scalar, so the two differ by how many scalars an element is.
    /// </summary>
    private static void WritePrimArray(IWriter writer, TypeTreeRepr element, object value, (NodeKind Kind, int Leaves) flat)
    {
        var align = element.RequiresAlign;
        switch (flat.Kind)
        {
            case NodeKind.SInt8: WriteShape(writer, (sbyte[])value, flat.Leaves, 1, align, static (w, v) => w.WriteInt8(v)); break;
            case NodeKind.UInt8: WriteShape(writer, (byte[])value, flat.Leaves, 1, align, static (w, v) => w.WriteUInt8(v)); break;
            case NodeKind.Int16: WriteShape(writer, (short[])value, flat.Leaves, 2, align, static (w, v) => w.WriteInt16(v)); break;
            case NodeKind.UInt16: WriteShape(writer, (ushort[])value, flat.Leaves, 2, align, static (w, v) => w.WriteUInt16(v)); break;
            case NodeKind.Int32: WriteShape(writer, (int[])value, flat.Leaves, 4, align, static (w, v) => w.WriteInt32(v)); break;
            case NodeKind.UInt32: WriteShape(writer, (uint[])value, flat.Leaves, 4, align, static (w, v) => w.WriteUInt32(v)); break;
            case NodeKind.Int64: WriteShape(writer, (long[])value, flat.Leaves, 8, align, static (w, v) => w.WriteInt64(v)); break;
            case NodeKind.UInt64: WriteShape(writer, (ulong[])value, flat.Leaves, 8, align, static (w, v) => w.WriteUInt64(v)); break;
            case NodeKind.Single: WriteShape(writer, (float[])value, flat.Leaves, 4, align, static (w, v) => w.WriteSingle(v)); break;
            case NodeKind.Double: WriteShape(writer, (double[])value, flat.Leaves, 8, align, static (w, v) => w.WriteDouble(v)); break;
            default:
                throw new NotSupportedException($"A {flat.Kind} vector of structures is not written as a run.");
        }
    }

    /// <summary>The length prefix is elements, the payload is scalars.</summary>
    private static void WriteShape<T>(IWriter writer, T[] values, int leaves, int elementSize, bool align,
        Action<IWriter, T> write) where T : unmanaged
    {
        writer.WriteInt32(values.Length / leaves);
        WriteRunValues(writer, values, elementSize, align, write);
    }

    private static void WriteStringRun(IWriter writer, string[] values, bool align)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values)
        {
            writer.WriteSizedString(value);
            if (align) writer.Align(4);
        }
    }
    
    public override string ToString() => ToString(0);

    /// <summary>
    /// A value tree as text, without the schema: a class or a vector prints one entry per slot, and a map prints its
    /// pairs. The member names are the tree's, which only the caller with the schema can supply, so this is the dump
    /// for a value whose schema is not at hand.
    /// </summary>
    public static string ObjectToString(object obj, int i = 0)
    {
        StringBuilder sb = new StringBuilder(); 
        switch (obj)
        {
            // A value the tree keeps as itself carries its own description.
            case IUnityObject concrete:
                sb.AppendLine(concrete.ToPlainText());
                break;
            case List<KeyValuePair<object, object>> pairs:
            {
                sb.AppendLine();
                foreach (var pair in pairs)
                {
                    sb.Append(new string('\t', i + 1));
                    sb.Append(pair.Key);
                    sb.Append(": ");
                    sb.AppendLine(ObjectToString(pair.Value, i + 1));
                }
                break;
            }
            case Array array:
            {
                sb.AppendLine();
                for (int j = 0; j < array.Length; j++)
                {
                    sb.Append(new string('\t', i + 1));
                    sb.AppendLine(ObjectToString(array.GetValue(j)!, i + 1));
                }
                break;
            }
            default:
            {
                sb.AppendLine($" {obj}");
                break;
            }
        }
        return sb.ToString();
    }
    
    public string ToString(int i)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"{new string('\t', i)}{Type} {Name} :");
        sb.Append(ObjectToString(Value, i));
        return sb.ToString();
    }
}
