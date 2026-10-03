using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Types.PreDefined.Types;

public class MonoBehaviour : IMonoBehaviour
{
    public string ClassName => "MonoBehaviour";
    public PPtr<GameObject> m_GameObject { get; }
    public byte m_Enabled { get; }
    public PPtr<IMonoScript> m_Script { get; }
    private bool __assetEditable = false;
    public MonoBehaviour Clone() => UnityAsset.NET.Types.AssetCloner.Clone(this);
    public string m_Name
    {
        get => NodeData.As<Dictionary<string, NodeData>>()["m_Name"].As<string>();
        set
        {
            if (!__assetEditable) throw new InvalidOperationException("Clone the asset before editing it.");
            NodeData.As<Dictionary<string, NodeData>>()["m_Name"].Value = value;
        }
    }
    private readonly NodeData _nodeData;
    public NodeData NodeData => __assetEditable ? _nodeData : UnityAsset.NET.Types.AssetCloner.CopyField(_nodeData);
    public TypeTreeRepr TypeTree { get; }

    public MonoBehaviour(IReader reader, TypeTreeRepr typeTree)
    {
        TypeTree = typeTree;
        _nodeData = new NodeData(reader, typeTree);
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
    }
    
    public void Write(IWriter writer)
    {
        NodeData.WriteValue(writer, TypeTree, NodeData.Value);
    }
    
    public string ToPlainText()
    {
        return NodeData.ToString();
    }
}
