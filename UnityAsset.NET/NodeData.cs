using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Types;
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
                            value = new List<NodeData>();
                            break;
                        }
                        var list = new List<NodeData>(size);
                        var array_node = vector.SubNodes[1];
                        for (int j = 0; j < size; j++)
                        {
                            list.Add(new NodeData(reader, array_node));
                        }
                        value = list;
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
        //Console.WriteLine($"{current.Type} {current.Name}: pos: {reader.Position}, align: {align}");
        if (align)
            reader.Align(4);
        return value;
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