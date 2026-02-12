using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class ComponentPair : IPreDefinedInterface
{
    public string ClassName => "ComponentPair";
    public PPtr<IComponent> component { get; }
    
    public ComponentPair(IReader reader)
    {
        component = new PPtr<IComponent>(reader);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var rootAssetNode = new AssetNode
        {
            Name = name,
            TypeName = "ComponentPair"
        };
        var childNode_component = component?.ToAssetNode("component");
        if (childNode_component != null)
        {
            rootAssetNode.Children.Add(childNode_component);
        }

        return rootAssetNode;
    }
}