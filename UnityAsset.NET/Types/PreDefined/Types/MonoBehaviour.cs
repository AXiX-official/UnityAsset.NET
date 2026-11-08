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

    /// <summary>The node tree's members, one slot per subnode of the tree, in the order the tree carries them.</summary>
    private readonly object[] _members;

    public MonoBehaviour(IReader reader, TypeTreeRepr typeTree)
    {
        _reader = reader;
        TypeTree = typeTree;
        NodeData = new NodeData(reader, typeTree);
        _members = (object[])NodeData.Value;
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
        get => (byte)Member("m_Enabled");
        set => _members[IndexOf("m_Enabled")] = value;
    }

    public PPtr<IMonoScript> m_Script
    {
        get => ReadPPtr<IMonoScript>("m_Script");
        set => WritePPtr("m_Script", value);
    }

    public string m_Name
    {
        get => (string)Member("m_Name");
        set => _members[IndexOf("m_Name")] = value;
    }

    public void Write(IWriter writer)
    {
        NodeData.WriteValue(writer, TypeTree, NodeData.Value);
    }

    public string ToPlainText()
    {
        return NodeData.ToString();
    }

    private object Member(string name) => _members[IndexOf(name)];

    private object[] Members(string name) => (object[])_members[IndexOf(name)];

    private int IndexOf(string name)
    {
        var subNodes = TypeTree.SubNodes;
        for (var index = 0; index < subNodes.Length; index++)
            if (subNodes[index].Name == name) return index;
        
        throw new KeyNotFoundException($"{TypeTree.TypeName} has no member named '{name}'.");
    }

    private PPtr<T> ReadPPtr<T>(string name) where T : IUnityObject
    {
        var members = Members(name);
        return new PPtr<T>((int)members[0], (long)members[1], _reader);
    }

    private void WritePPtr(string name, IPPtr pptr)
    {
        var members = Members(name);
        members[0] = pptr.m_FileID;
        members[1] = pptr.m_PathID;
    }
}
