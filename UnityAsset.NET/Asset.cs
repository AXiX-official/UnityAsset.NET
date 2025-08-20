using System.Collections.Specialized;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public IReader RawData;
    //public NodeData NodeData;
    public IAsset Value;

    public Asset(AssetFileInfo info, IReader reader)
    {
        Info = info;
        RawData = reader; 
        Value = UnityObjectFactory.Create(info.Type, reader);
        //NodeData = new NodeData(reader, info.Type.Nodes, info.Type.Nodes[0]);
    }
}