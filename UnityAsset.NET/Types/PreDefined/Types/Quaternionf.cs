using UnityAsset.NET.IO;

namespace UnityAsset.NET.Types.PreDefined.Types;

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
    
    public void Write(IWriter writer)
    {
        writer.WriteSingle(x);
        writer.WriteSingle(y);
        writer.WriteSingle(z);
        writer.WriteSingle(w);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Quaternionf"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = y });
        root.Children.Add(new AssetNode { Name = "z", TypeName = "float", Value = z });
        root.Children.Add(new AssetNode { Name = "w", TypeName = "float", Value = w });
        return root;
    }
}
