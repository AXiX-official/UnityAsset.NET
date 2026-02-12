using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class BoneWeights4 : IPreDefinedInterface
{
    public string ClassName => "BoneWeights4";
    public float[] weight = new float[4];
    public int[] boneIndex = new int[4];
    
    public float weight_0_ => weight[0];
    public float weight_1_ => weight[1];
    public float weight_2_ => weight[2];
    public float weight_3_ => weight[3];
    public int boneIndex_0_ => boneIndex[0];
    public int boneIndex_1_ => boneIndex[1];
    public int boneIndex_2_ => boneIndex[2];
    public int boneIndex_3_ => boneIndex[3];

    public BoneWeights4()
    {
        
    }
    
    public BoneWeights4(IReader reader)
    {
        reader.ReadFixedArray(weight, r => r.ReadSingle());
        reader.ReadFixedArray(boneIndex, r => r.ReadInt32());
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "BoneWeights4"
        };
        root.Children.Add(new AssetNode { Name = "weight[0]", TypeName = "float", Value = weight_0_ });
        root.Children.Add(new AssetNode { Name = "weight[1]", TypeName = "float", Value = weight_1_ });
        root.Children.Add(new AssetNode { Name = "weight[2]", TypeName = "float", Value = weight_2_ });
        root.Children.Add(new AssetNode { Name = "weight[3]", TypeName = "float", Value = weight_3_ });
        root.Children.Add(new AssetNode { Name = "boneIndex[0]", TypeName = "int", Value = boneIndex_0_ });
        root.Children.Add(new AssetNode { Name = "boneIndex[1]", TypeName = "int", Value = boneIndex_1_ });
        root.Children.Add(new AssetNode { Name = "boneIndex[2]", TypeName = "int", Value = boneIndex_2_ });
        root.Children.Add(new AssetNode { Name = "boneIndex[3]", TypeName = "int", Value = boneIndex_3_ });
        return root;
    }
}