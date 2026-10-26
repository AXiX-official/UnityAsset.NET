using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Types.PreDefined.Types;

public class MonoBehaviour : IMonoBehaviour
{
    public string ClassName => "MonoBehaviour";
    public NodeData NodeData { get; }
    public TypeTreeRepr TypeTree { get; }

    private readonly IReader _reader;
    private readonly Dictionary<string, NodeData> _members;

    public MonoBehaviour(IReader reader, TypeTreeRepr typeTree)
    {
        _reader = reader;
        TypeTree = typeTree;
        NodeData = new NodeData(reader, typeTree);
        _members = NodeData.As<Dictionary<string, NodeData>>();
    }

    // The typed members are views over the node tree, which is what actually serializes: a setter writes the node so
    // that a value set here and a value set through NodeData cannot disagree.
    public PPtr<GameObject> m_GameObject
    {
        get => ReadPPtr<GameObject>("m_GameObject");
        set => WritePPtr("m_GameObject", value);
    }

    public byte m_Enabled
    {
        get => _members["m_Enabled"].As<byte>();
        set => _members["m_Enabled"].Value = value;
    }

    public PPtr<IMonoScript> m_Script
    {
        get => ReadPPtr<IMonoScript>("m_Script");
        set => WritePPtr("m_Script", value);
    }

    public string m_Name
    {
        get => _members["m_Name"].As<string>();
        set => _members["m_Name"].Value = value;
    }

    public void Write(IWriter writer)
    {
        NodeData.WriteValue(writer, TypeTree, NodeData.Value);
    }

    public string ToPlainText()
    {
        return NodeData.ToString();
    }

    private PPtr<T> ReadPPtr<T>(string name) where T : IUnityObject
    {
        var members = _members[name].As<Dictionary<string, NodeData>>();
        return new PPtr<T>(members["m_FileID"].As<int>(), members["m_PathID"].As<long>(), _reader);
    }

    private void WritePPtr(string name, IPPtr pptr)
    {
        var members = _members[name].As<Dictionary<string, NodeData>>();
        members["m_FileID"].Value = pptr.m_FileID;
        members["m_PathID"].Value = pptr.m_PathID;
    }
}
