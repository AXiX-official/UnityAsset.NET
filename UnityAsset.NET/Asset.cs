using System.Collections.Specialized;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public DataBuffer RawData;
    public NodeData NodeData;

    public Asset(AssetFileInfo info, DataBuffer db)
    {
        Info = info;
        RawData = db;
        NodeData = new NodeData(db, info.Type.Nodes, info.Type.Nodes[0]);
    }
}