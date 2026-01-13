using System.Diagnostics.CodeAnalysis;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined;

namespace UnityAsset.NET;

public class Asset
{ 
    public AssetFileInfo Info;
    public IReader RawData;
    private IUnityAsset? _value;
    private string _name = string.Empty;
    private readonly object _lock = new();
    private readonly bool _isNamedAsset;
    private readonly int _nameFieldIndex;
    
    public SerializedFile? SourceFile { get; private set; }

    public IUnityAsset Value
    {
        get
        {
            lock (_lock)
            {
                if (_value == null)
                {
                    RawData.Seek(0);
                    _value = UnityObjectFactory.Create(Info.Type, RawData);
                    if (_isNamedAsset)
                    {
                        _name = ((INamedAsset)_value).m_Name;
                    }
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
                if (_isNamedAsset && string.IsNullOrEmpty(_name))
                {
                    if (_nameFieldIndex == 1)
                    {
                        RawData.Seek(0);
                        _name = RawData.ReadSizedString();
                        RawData.Seek(0);
                    }
                }
                return _name;
            }
        }
    }
    
    public long Size => Info.ByteSize;

    public long PathId => Info.PathId;

    public string Container
    {
        get
        {
            var containers = SourceFile?.Containers;
            if (containers != null && containers.TryGetValue(PathId, out var container))
            {
                return container;
            }
            return string.Empty;
        }
    }

    public Asset(SerializedFile sf, AssetFileInfo info, IReader reader)
    {
        Info = info;
        RawData = reader; 
        _isNamedAsset = info.Type.Nodes.Any(n => n is {Name: "m_Name", Type: "string", Level: 1} );
        _nameFieldIndex = _isNamedAsset ? info.Type.Nodes.FindIndex(n => n.Name == "m_Name") : -1;

        SourceFile = sf;
    }

    public void Release()
    {
        lock (_lock)
        {
            _value = null;
        }
    }
}