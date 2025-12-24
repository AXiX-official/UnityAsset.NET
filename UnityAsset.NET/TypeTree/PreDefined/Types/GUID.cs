using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class GUID : IGUID, IEquatable<GUID>
{
    public string ClassName => "GUID";
    public UInt32 data_0_ { get; }
    public UInt32 data_1_ { get; }
    public UInt32 data_2_ { get; }
    public UInt32 data_3_ { get; }

    public GUID(IReader reader)
    {
        data_0_ = reader.ReadUInt32();
        data_1_ = reader.ReadUInt32();
        data_2_ = reader.ReadUInt32();
        data_3_ = reader.ReadUInt32();
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "GUID"
        };
        root.Children.Add(new AssetNode { Name = "data[0]", TypeName = "unsigned int", Value = this.data_0_ });
        root.Children.Add(new AssetNode { Name = "data[1]", TypeName = "unsigned int", Value = this.data_1_ });
        root.Children.Add(new AssetNode { Name = "data[2]", TypeName = "unsigned int", Value = this.data_2_ });
        root.Children.Add(new AssetNode { Name = "data[3]", TypeName = "unsigned int", Value = this.data_3_ });
        return root;
    }
    
    public bool Equals(GUID? other)
    {
        if (other is null) return false;
        return data_0_ == other.data_0_ &&
               data_1_ == other.data_1_ &&
               data_2_ == other.data_2_ &&
               data_3_ == other.data_3_;
    }
}