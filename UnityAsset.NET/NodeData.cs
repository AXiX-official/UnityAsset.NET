using System.Collections;
using System.Collections.Specialized;
using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.SerializedFiles;

namespace UnityAsset.NET;

public class NodeData
{
    public byte Level;
    public string Type;
    public string Name;
    public object Value;
    public UInt32 MetaFlags;
    public NodeData? Parent;
    public List<NodeData>? Children;

    public NodeData(byte level, string type, string name, object value, UInt32 metaFlags, NodeData? parent = null, List<NodeData>? children = null)
    {
        Level = level;
        Type = type;
        Name = name;
        Value = value;
        MetaFlags = metaFlags;
        Parent = parent;
        Children = children;
    }
    
    public NodeData(TypeTreeNode typeTreeNode)
    {
        Level = typeTreeNode.Level;
        Type = typeTreeNode.Type;
        Name = typeTreeNode.Name;
        MetaFlags = typeTreeNode.MetaFlags;
    }
    
    public static object ReadValue(List<NodeData> m_Nodes, HeapDataBuffer hdb, ref int i)
    {
        object value;
        var node = m_Nodes[i];
        var align = (node.MetaFlags & 0x4000) != 0;
        switch (node.Type)
        {
            case "SInt8":
                value = hdb.ReadInt8();
                break;
            case "UInt8":
                value = hdb.ReadUInt8();
                break;
            case "char":
                value = BitConverter.ToChar(hdb.ReadBytes(2), 0);
                break;
            case "short":
            case "SInt16":
                value = hdb.ReadInt16();
                break;
            case "UInt16":
            case "unsigned short":
                value = hdb.ReadUInt16();
                break;
            case "int":
            case "SInt32":
                value = hdb.ReadInt32();
                break;
            case "UInt32":
            case "unsigned int":
            case "Type*":
                value = hdb.ReadUInt32();
                break;
            case "long long":
            case "SInt64":
                value = hdb.ReadInt64();
                break;
            case "UInt64":
            case "unsigned long long":
            case "FileSize":
                value = hdb.ReadUInt64();
                break;
            case "float":
                value = hdb.ReadFloat();
                break;
            case "double":
                value = hdb.ReadDouble();
                break;
            case "bool":
                value = hdb.ReadBoolean();
                break;
            case "string":
                value = hdb.ReadAlignedString();
                var toSkip = GetNodes(m_Nodes, i);
                i += toSkip.Count - 1;
                break;
            case "map":
                {
                    if ((m_Nodes[i + 1].MetaFlags & 0x4000) != 0)
                        align = true;
                    var map = GetNodes(m_Nodes, i);
                    i += map.Count - 1;
                    var first = GetNodes(map, 4);
                    var next = 4 + first.Count;
                    var second = GetNodes(map, next);
                    var size = hdb.ReadInt32();
                    var dic = new List<KeyValuePair<object, object>>();
                    for (int j = 0; j < size; j++)
                    {
                        int tmp1 = 0;
                        int tmp2 = 0;
                        dic.Add(new KeyValuePair<object, object>(ReadValue(first, hdb, ref tmp1), ReadValue(second, hdb, ref tmp2)));
                    }
                    value = dic;
                    break;
                }
            case "TypelessData":
                {
                    var size = hdb.ReadInt32();
                    value = hdb.ReadBytes(size);
                    i += 2;
                    break;
                }
            default:
                {
                    if (i < m_Nodes.Count - 1 && m_Nodes[i + 1].Type == "Array") //Array
                    {
                        if ((m_Nodes[i + 1].MetaFlags & 0x4000) != 0)
                            align = true;
                        var vector = GetNodes(m_Nodes, i);
                        i += vector.Count - 1;
                        var size = hdb.ReadInt32();
                        var list = new List<object>();
                        for (int j = 0; j < size; j++)
                        {
                            int tmp = 3;
                            list.Add(ReadValue(vector, hdb, ref tmp));
                        }
                        value = list;
                        break;
                    }
                    else //Class
                    {
                        var @class = GetNodes(m_Nodes, i);
                        i += @class.Count - 1;
                        var obj = new OrderedDictionary();
                        for (int j = 1; j < @class.Count; j++)
                        {
                            var classmember = @class[j];
                            var name = classmember.Name;
                            var subValue = ReadValue(@class, hdb, ref j);
                            classmember.Value = subValue;
                            obj[name] = subValue;
                        }
                        value = obj;
                        break;
                    }
                }
        }
        if (align)
            hdb.Align(4);
        return value;
    }
    
    private static List<NodeData> GetNodes(List<NodeData> m_Nodes, int index)
    {
        var nodes = new List<NodeData>();
        nodes.Add(m_Nodes[index]);
        var level = m_Nodes[index].Level;
        for (int i = index + 1; i < m_Nodes.Count; i++)
        {
            var member = m_Nodes[i];
            var level2 = member.Level;
            if (level2 <= level)
            {
                return nodes;
            }
            nodes.Add(member);
        }
        return nodes;
    }
    
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Type: {Type} | ");
        sb.Append($"Name: {Name} | ");
        if (Value is OrderedDictionary odValue)
        {
            sb.AppendLine();
            foreach (DictionaryEntry entry in odValue)
            {
                sb.AppendLine($"Key: {entry.Key}, Value: {entry.Value}");
            }
        }
        else
        {
            sb.Append($"Value: {Value}");
        }
        return sb.ToString();
    }
}