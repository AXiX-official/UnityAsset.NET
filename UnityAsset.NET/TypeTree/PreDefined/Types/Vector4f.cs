using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class Vector4f : IVector4f
{
    public string ClassName => "Vector4f";
    public float x { get; }
    public float y { get; }
    public float z { get; }
    public float w { get; }

    public Vector4f(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
        w = reader.ReadFloat();
    }
    
    public Vector4f(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Vector4f"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = this.x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = this.y });
        root.Children.Add(new AssetNode { Name = "z", TypeName = "float", Value = this.z });
        root.Children.Add(new AssetNode { Name = "w", TypeName = "float", Value = this.w });
        return root;
    }
}