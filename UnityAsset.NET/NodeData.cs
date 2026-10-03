using System.Text;
using UnityAsset.NET.IO;
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
        switch (current.TypeName)
        {
            case "SInt8":
                value = reader.ReadInt8();
                break;
            case "UInt8":
                value = reader.ReadUInt8();
                break;
            case "char":
                value = BitConverter.ToChar(reader.ReadBytes(2), 0);
                break;
            case "short":
            case "SInt16":
                value = reader.ReadInt16();
                break;
            case "UInt16":
            case "unsigned short":
                value = reader.ReadUInt16();
                break;
            case "int":
            case "SInt32":
                value = reader.ReadInt32();
                break;
            case "UInt32":
            case "unsigned int":
            case "Type*":
                value = reader.ReadUInt32();
                break;
            case "long long":
            case "SInt64":
                value = reader.ReadInt64();
                break;
            case "UInt64":
            case "unsigned long long":
            case "FileSize":
                value = reader.ReadUInt64();
                break;
            case "float":
                value = reader.ReadSingle();
                break;
            case "double":
                value = reader.ReadDouble();
                break;
            case "bool":
                value = reader.ReadBoolean();
                break;
            case "string":
                value = reader.ReadSizedString();
                break;
            case "map":
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
                }
                value = dic;
                break;
                }
            case "TypelessData":
                {
                    value = new TypelessData(reader);
                    break;
                }
            default:
                {
                    if (current.SubNodes.Length == 1 && current.SubNodes[0].TypeName == "Array") //Array
                    {
                        var vector = current.SubNodes[0];
                        align |= vector.RequiresAlign;
                        var size = reader.ReadInt32();
                        if (size == 0)
                        {
                            value = Array.Empty<NodeData>();
                            break;
                        }
                        var array = new NodeData[size];
                        var arrayNode = vector.SubNodes[1];
                        for (int j = 0; j < size; j++)
                        {
                            array[j] = new NodeData(reader, arrayNode);
                        }
                        value = array;
                        break;
                    }
                    else //Class
                    {
                        var @class = current.SubNodes;
                        var obj = new Dictionary<string, NodeData>();
                        for (int j = 0; j < @class.Length; j++)
                        {
                            var classmember = @class[j];
                            var name = classmember.Name;
                            obj[name] = new NodeData(reader, @class[j]);
                        }
                        value = obj;
                        break;
                    }
                }
        }
        if (align)
            reader.Align(4);
        return value;
    }
    
    public static void WriteValue(IWriter writer, TypeTreeRepr current, object value)
    {
        var align = current.RequiresAlign;
        switch (current.TypeName)
        {
            case "SInt8":
                writer.WriteInt8((sbyte)value);
                break;
            case "UInt8":
                writer.WriteUInt8((byte)value);
                break;
            case "char":
                writer.WriteChar((char)value);
                break;
            case "short":
            case "SInt16":
                writer.WriteInt16((short)value);
                break;
            case "UInt16":
            case "unsigned short":
                writer.WriteUInt16((ushort)value);
                break;
            case "int":
            case "SInt32":
                writer.WriteInt32((int)value);
                break;
            case "UInt32":
            case "unsigned int":
            case "Type*":
                writer.WriteUInt32((uint)value);
                break;
            case "long long":
            case "SInt64":
                writer.WriteInt64((long)value);
                break;
            case "UInt64":
            case "unsigned long long":
            case "FileSize":
                writer.WriteUInt64((ulong)value);
                break;
            case "float":
                writer.WriteSingle((float)value);
                break;
            case "double":
                writer.WriteDouble((double)value);
                break;
            case "bool":
                writer.WriteBoolean((bool)value);
                break;
            case "string":
                writer.WriteSizedString((string)value);
                break;
            case "map":
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
                    }
                    break;
                }
            case "TypelessData":
                ((TypelessData)value).Write(writer);
                break;
            default:
                {
                    if (current.SubNodes.Length == 1 && current.SubNodes[0].TypeName == "Array") //Array
                    {
                        var vector = current.SubNodes[0];
                        align |= vector.RequiresAlign;
                        var array = (NodeData[])value;
                        writer.WriteInt32(array.Length);
                        var arrayNode = vector.SubNodes[1];
                        foreach (var item in array)
                        {
                            WriteValue(writer, arrayNode, item.Value);
                        }
                        break;
                    }
                    else //Class
                    {
                        var members = (Dictionary<string, NodeData>)value;
                        foreach (var member in current.SubNodes)
                        {
                            WriteValue(writer, member, members[member.Name].Value);
                        }
                        break;
                    }
                }
        }
        if (align)
            writer.Align(4);
    }
    
    public override string ToString() => ToString(0);

    public static string ObjectToString(object obj, int i = 0)
    {
        StringBuilder sb = new StringBuilder(); 
        switch (obj)
        {
            case List<NodeData> cls:
            {
                sb.AppendLine();
                foreach (var member in cls)
                {
                    sb.Append(member.ToString(i + 1));
                }
                break;
            }
            case byte[] bytes:
            {
                var span = bytes.AsSpan();
                sb.AppendLine();
                sb.Append(new string('\t', i + 1));
                sb.Append("[");
                for (int j = 0; j < span.Length; j++)
                {
                    if (j > 0) sb.Append(", ");
                    sb.Append(span[j]);
                }
                sb.AppendLine("]");
                break;
            }
            case Dictionary<string, NodeData> dict:
            {
                sb.AppendLine();
                foreach (var (key, value) in dict)
                {
                    sb.Append(value.ToString(i + 1));
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
