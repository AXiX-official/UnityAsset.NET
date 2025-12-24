using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class SecondarySpriteTexture : ISecondarySpriteTexture
{
    public string ClassName => "SecondarySpriteTexture";
    public PPtr<ITexture2D> texture { get; }
    public string name { get; }

    public SecondarySpriteTexture(IReader reader)
    {
        texture = new PPtr<ITexture2D>(reader);
        name = reader.ReadSizedString();
        reader.Align(4);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "SecondarySpriteTexture"
        };
        var childNode_texture = texture.ToAssetNode("texture");
        if (childNode_texture != null)
        {
            root.Children.Add(childNode_texture);
        }

        root.Children.Add(new AssetNode { Name = "name", TypeName = "string", Value = this.name });
        return root;
    }
}