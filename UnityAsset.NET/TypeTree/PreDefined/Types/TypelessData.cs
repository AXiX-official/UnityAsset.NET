using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class TypelessData : IPreDefinedObject
{
    public Int32 size { get; }
    public byte[] data { get; }
    
    public TypelessData(IReader reader)
    {
        size = reader.ReadInt32();
        data = reader.ReadBytes(size);
    }
    
    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "TypelessData"
        };
        root.Children.Add(new AssetNode { Name = "size", TypeName = "int", Value = this.size });
        root.Children.Add(new AssetNode { Name = "data", TypeName = "UInt8", Value = this.data });
        return root;
    }

    public string ClassName => "TypelessData";
}