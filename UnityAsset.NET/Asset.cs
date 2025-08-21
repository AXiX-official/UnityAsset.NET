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
    private IAsset? value;

    public IAsset Value
    {
        get
        {
            if (value == null)
            {
                value = UnityObjectFactory.Create(Info.Type, RawData);;
            }
            return value;
        }
    }

    public string Type => Value.ClassName;

    public string Name
    {
        get {
            if (Value is INamedAsset named)
            {
                return named.m_Name;
            }
            return string.Empty;
        }
    }

    public Asset(AssetFileInfo info, IReader reader)
    {
        Info = info;
        RawData = reader; 
        //NodeData = new NodeData(reader, info.Type.Nodes, info.Type.Nodes[0]);
    }
}