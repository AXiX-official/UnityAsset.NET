using System.Collections.Specialized;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public IReader RawData;
    public NodeData NodeData;

    public Asset(AssetFileInfo info, IReader reader)
    {
        Info = info;
        RawData = reader;
        NodeData = new NodeData(reader, info.Type.Nodes, info.Type.Nodes[0]);
    }
}