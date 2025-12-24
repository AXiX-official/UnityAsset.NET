using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class Rectf : IRectf
{
    public string ClassName => "Rectf";
    public float x { get; }
    public float y { get; }
    public float width { get; }
    public float height { get; }

    public Rectf(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        width = reader.ReadFloat();
        height = reader.ReadFloat();
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Rectf"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = this.x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = this.y });
        root.Children.Add(new AssetNode { Name = "width", TypeName = "float", Value = this.width });
        root.Children.Add(new AssetNode { Name = "height", TypeName = "float", Value = this.height });
        return root;
    }
}