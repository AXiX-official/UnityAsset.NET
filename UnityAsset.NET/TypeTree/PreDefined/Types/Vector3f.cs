using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class Vector3f : IVector3f
{
    public string ClassName => "Vector3f";
    public float x { get; }
    public float y { get; }
    public float z { get; }

    public Vector3f(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
    }

    public Vector3f(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "Vector3f"
        };
        root.Children.Add(new AssetNode { Name = "x", TypeName = "float", Value = this.x });
        root.Children.Add(new AssetNode { Name = "y", TypeName = "float", Value = this.y });
        root.Children.Add(new AssetNode { Name = "z", TypeName = "float", Value = this.z });
        return root;
    }
}