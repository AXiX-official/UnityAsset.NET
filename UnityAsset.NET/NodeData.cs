using System.Collections;
using System.Collections.Specialized;
using System.Text;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET;

public class NodeData
{
    public byte Level;
    public string Type;
    public string Name;
    public object Value;
    public UInt32 MetaFlags;
    public NodeData? Parent;

    public NodeData(byte level, string type, string name, object value, UInt32 metaFlags, NodeData? parent = null)
    {
        Level = level;
        Type = type;
        Name = name;
        Value = value;
        MetaFlags = metaFlags;
        Parent = parent;
    }
    
    public NodeData(IReader reader, List<TypeTreeNode> nodes, TypeTreeNode current, NodeData? parent = null)
    {
        Level = current.Level;
        Type = current.Type;
        Name = current.Name;
        MetaFlags = current.MetaFlags;
        Parent = parent;
        Value = ReadValue(reader, nodes, current);
    }
    
    public static object ReadValue(IReader reader, List<TypeTreeNode> nodes, TypeTreeNode current)
    {
        object value;
        var align = current.RequiresAlign();
        switch (current.Type)
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
                value = reader.ReadFloat();
                break;
            case "double":
                value = reader.ReadDouble();
                break;
            case "bool":
                value = reader.ReadBoolean();
                break;
            case "string":
                align |= current.Children(nodes)?[0].RequiresAlign()?? false;
                value = reader.ReadSizedString();
                break;
            case "map":
            {
                var pair = current.Children(nodes)?[0].Children(nodes)?[1];
                align |= pair?.RequiresAlign() ?? false;
                var first = pair.Children(nodes)?[0];
                var second = pair.Children(nodes)?[1];
                var size = reader.ReadInt32();
                var dic = new List<KeyValuePair<object, object>>();
                for (int j = 0; j < size; j++)
                {
                    dic.Add(new KeyValuePair<object, object>(ReadValue(reader, nodes, first), ReadValue(reader, nodes, second)));
                }
                value = dic;
                break;
                }
            case "TypelessData":
                {
                    var size = reader.ReadInt32();
                    value = reader.ReadBytes(size);
                    break;
                }
            default:
                {
                    if (current.Children(nodes).Count == 1 && current.Children(nodes)[0].Type == "Array") //Array
                    {
                        var vector = current.Children(nodes)[0];
                        align |= vector.RequiresAlign();
                        var size = reader.ReadInt32();
                        if (size == 0)
                        {
                            value = null;
                            break;
                        }
                        var list = new List<NodeData>();
                        var array_node = vector.Children(nodes)[1];
                        for (int j = 0; j < size; j++)
                        {
                            list.Add(new NodeData(reader, nodes, array_node));
                        }
                        value = list;
                        break;
                    }
                    else //Class
                    {
                        var @class = current.Children(nodes);
                        var obj = new List<NodeData>();
                        for (int j = 0; j < @class.Count; j++)
                        {
                            var classmember = @class[j];
                            var name = classmember.Name;
                            obj.Add(new NodeData(reader, nodes, @class[j]));
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