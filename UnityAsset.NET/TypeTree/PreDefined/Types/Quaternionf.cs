using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public struct Quaternionf : IPreDefinedInterface
{
    public string ClassName => "Quaternionf";
    
    public float x { get; }
    public float y { get; }
    public float z { get; }
    public float w { get; }

    public Quaternionf(float x = 0, float y = 0, float z = 0, float w = 0)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    
    public Quaternionf(IReader reader)
    {
        x = reader.ReadSingle();
        y = reader.ReadSingle();
        z = reader.ReadSingle();
        w = reader.ReadSingle();
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