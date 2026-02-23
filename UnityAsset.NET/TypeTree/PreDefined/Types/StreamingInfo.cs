using System.Diagnostics.CodeAnalysis;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class StreamingInfo  : IPreDefinedObject
{
    public string ClassName => "StreamingInfo";
    public UInt64 offset { get; }
    public UInt32 size { get; }
    public string path { get; }
    
    public StreamingInfo(IReader reader)
    {
        offset = (UnityRevision)((AssetReader)reader).AssetsFile.Metadata.UnityVersion >= "2020" ? reader.ReadUInt64() : reader.ReadUInt32();
        size = reader.ReadUInt32();
        path = reader.ReadSizedString();
        reader.Align(4);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "StreamingInfo"
        };
        root.Children.Add(new AssetNode { Name = "offset", TypeName = "UInt64", Value = this.offset });
        root.Children.Add(new AssetNode { Name = "size", TypeName = "unsigned int", Value = this.size });
        root.Children.Add(new AssetNode { Name = "path", TypeName = "string", Value = this.path });
        return root;
    }

    public bool TryGetData(AssetManager mgr, [NotNullWhen(true)]out TypelessData? data)
    {
        data = null;
        var filePath = path.Split("/")[^1];
        if (mgr.LoadedFiles.TryGetValue(filePath, out var file))
        {
            if (file is IReaderProvider rp)
            {
                var reader = rp.CreateReader();
                reader.Position = (long)offset;
                data = new TypelessData((int)size, reader.ReadBytes((int)size));
                return true;
            }
        }
        return false;
    }
}