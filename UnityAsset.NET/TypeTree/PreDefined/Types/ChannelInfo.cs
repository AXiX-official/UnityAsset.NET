using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public struct ChannelInfo : IPreDefinedInterface
{
    public string ClassName => "ChannelInfo";
    public byte stream { get; }
    public byte offset { get; }
    public byte format { get; }
    public byte dimension { get; }
    
    public ChannelInfo(IReader reader)
    {
        stream = reader.ReadByte();
        offset = reader.ReadByte();
        format = reader.ReadByte();
        dimension = (Byte)(reader.ReadByte() & 0xF);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var rootAssetNode = new AssetNode
        {
            Name = name,
            TypeName = "ChannelInfo"
        };
        rootAssetNode.Children.Add(new AssetNode { Name = "stream", TypeName = "UInt8", Value = stream });
        rootAssetNode.Children.Add(new AssetNode { Name = "offset", TypeName = "UInt8", Value = offset });
        rootAssetNode.Children.Add(new AssetNode { Name = "format", TypeName = "UInt8", Value = format });
        rootAssetNode.Children.Add(new AssetNode { Name = "dimension", TypeName = "UInt8", Value = dimension });
        return rootAssetNode;
    }
}