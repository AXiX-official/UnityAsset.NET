using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class GameObject : INamedObject
{
    public string ClassName => "GameObject";
    public List<ComponentPair> m_Component { get; }
    public UInt32 m_Layer { get; }
    public String m_Name { get; }
    public UInt16 m_Tag { get; }
    public Boolean m_IsActive { get; }

    public GameObject(IReader reader)
    {
        m_Component = reader.ReadListWithAlign<ComponentPair>(r => new ComponentPair(r), false);
        reader.Align(4);
        m_Layer = reader.ReadUInt32();
        m_Name = reader.ReadSizedString();
        reader.Align(4);
        m_Tag = reader.ReadUInt16();
        m_IsActive = reader.ReadBoolean();
    }

    public AssetNode ToAssetNode(string name = "Base")
    {
        var rootAssetNode = new AssetNode
        {
            Name = name,
            TypeName = "GameObject"
        };
        var m_ComponentVectorNode = new AssetNode
        {
            Name = "m_Component",
            TypeName = "vector"
        };
        foreach (var itemm_Component in m_Component)
        {
            var childNode_itemm_Component = itemm_Component.ToAssetNode("data");
            if (childNode_itemm_Component != null)
            {
                m_ComponentVectorNode.Children.Add(childNode_itemm_Component);
            }
        }

        rootAssetNode.Children.Add(m_ComponentVectorNode);
        rootAssetNode.Children.Add(new AssetNode { Name = "m_RuntimeCompatibility", TypeName = "unsigned int", Value = m_Layer });
        rootAssetNode.Children.Add(new AssetNode { Name = "m_Name", TypeName = "string", Value = m_Name });
        rootAssetNode.Children.Add(new AssetNode { Name = "curveCount", TypeName = "UInt16", Value = m_Tag });
        rootAssetNode.Children.Add(new AssetNode { Name = "m_Legacy", TypeName = "bool", Value = m_IsActive });
        return rootAssetNode;
    }
    
    public ITransform? m_Transform { get; set; }
    public IMeshRenderer? m_MeshRenderer { get; set; }
    public IMeshFilter? m_MeshFilter { get; set; }
    public ISkinnedMeshRenderer? m_SkinnedMeshRenderer { get; set; }
    public IAnimator? m_Animator { get; set; }
    public IAnimation? m_Animation { get; set; }

    public void Proccess(AssetManager mgr)
    {
        foreach (var componentPair in m_Component)
        {
            if (componentPair.component.TryGet(mgr, out var component))
            {
                switch (component)
                {
                    case ITransform t: m_Transform = t; break;
                    case IMeshRenderer mr: m_MeshRenderer = mr; break;
                    case IMeshFilter mf: m_MeshFilter = mf; break;
                    case ISkinnedMeshRenderer smr: m_SkinnedMeshRenderer = smr; break;
                    case IAnimator animator: m_Animator = animator; break;
                    case IAnimation animation: m_Animation = animation; break;
                }
            }
        }
    }
}