using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.TypeTreeHelper.PreDefined;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public IReader RawData;
    //public NodeData NodeData;
    private IAsset? _value;
    private string _name = string.Empty;
    private readonly object _lock = new();

    public IAsset Value
    {
        get
        {
            lock (_lock)
            {
                if (_value == null)
                {
                    RawData.Seek(0);
                    _value = UnityObjectFactory.Create(Info.Type, RawData);
                }
                return _value;
            }
        }
    }

    public string Type => Info.Type.Nodes[0].Type;

    public string Name
    {
        get
        {
            lock (_lock)
            {
                var nodes = Info.Type.Nodes;
                if (TypeTreeHelper.Compiler.Helper.IsNamedAsset(nodes[0], nodes) && string.IsNullOrEmpty(_name))
                {
                    RawData.Seek(0);
                    _name = RawData.ReadSizedString();
                    RawData.Seek(0);
                }
                return _name;
            }
        }
    }

    public Asset(AssetFileInfo info, IReader reader)
    {
        Info = info;
        RawData = reader; 
        //NodeData = new NodeData(reader, info.Type.Nodes, info.Type.Nodes[0]);
    }
}