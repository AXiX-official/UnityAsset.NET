using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class Quaternionf : IPreDefinedInterface
{
    public string ClassName => "Quaternionf";
    
    public float x { get; }
    public float y { get; }
    public float z { get; }
    public float w { get; }
    
    public Quaternionf(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
        w = reader.ReadFloat();
    }
    
    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Quaternionf"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = this.x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = this.y });
        root.Children.Add(new AssetNode { Name = "z", TypeName = "float", Value = this.z });
        root.Children.Add(new AssetNode { Name = "w", TypeName = "float", Value = this.w });
        return root;
    }
}