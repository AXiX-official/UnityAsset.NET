using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class MonoBehaviour : IMonoBehaviour
{
    public string ClassName => "MonoBehaviour";
    public PPtr<GameObject> m_GameObject { get; }
    public byte m_Enabled { get; }
    public PPtr<IMonoScript> m_Script { get; }
    public string m_Name { get; }
    public NodeData NodeData { get; }

    public MonoBehaviour(IReader reader, List<TypeTreeNode> nodes)
    {
        NodeData = new NodeData(reader, nodes, nodes[0]);
        var @class = NodeData.As<Dictionary<string, NodeData>>();
        var m_GameObjectClass = @class["m_GameObject"].As<Dictionary<string, NodeData>>();
        m_GameObject = new PPtr<GameObject>(
            m_GameObjectClass["m_FileID"].As<int>(),
            m_GameObjectClass["m_PathID"].As<long>(),
            reader
        );
        m_Enabled = @class["m_Enabled"].As<byte>();
        var m_ScriptClass = @class["m_Script"].As<Dictionary<string, NodeData>>();
        m_Script = new PPtr<IMonoScript>(
            m_ScriptClass["m_FileID"].As<int>(),
            m_ScriptClass["m_PathID"].As<long>(),
            reader
        );
        m_Name = @class["m_Name"].As<string>();
    }
    
    public string ToPlainText()
    {
        return NodeData.ToString();
    }
}