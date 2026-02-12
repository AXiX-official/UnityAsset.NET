using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public struct Vector2f : IPreDefinedInterface
{
    public string ClassName => "Vector2f";
    public float x { get; }
    public float y { get; }

    public Vector2f(IReader reader)
    {
        x = reader.ReadSingle();
        y = reader.ReadSingle();
    }

    public Vector2f(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Vector2f"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = this.x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = this.y });
        return root;
    }
    
    public static explicit operator Vector2f(Vector3f v)
    {
        return new Vector2f(v.x, v.y);
    }
}