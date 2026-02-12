using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined;

namespace UnityAsset.NET;

public class Asset
{
    private readonly AssetFileInfo _info;
    private IUnityAsset? _value;
    private string? _name;
    private readonly Lock _lock = new();
    private bool _isNamedAsset;

    private AssetReader DataReader
    {
        get
        {
            var sf = SourceFile;
            var readerProvider = sf.ReaderProvider;
            var start = sf.Header.DataOffset + _info.ByteOffset;
            var length = _info.ByteSize;
            var endian = sf.Header.Endianness;
            return new AssetReader(readerProvider, start, length, sf, endian);
        }
    }
    
    public SerializedFile SourceFile { get; }

    public IUnityAsset Value
    {
        get
        {
            lock (_lock)
            {
                if (_value == null)
                {
                    _value = UnityObjectFactory.Create(_info.Type, DataReader);
                    if (_isNamedAsset)
                    {
                        _name = ((INamedObject)_value).m_Name;
                    }
                }
                return _value;
            }
        }
    }

    public string Type => _info.Type.ToTypeName();

    public string Name
    {
        get
        {
            lock (_lock)
            {
                if (!_isNamedAsset)
                    return string.Empty;

                _name ??= ((INamedObject)Value).m_Name;

                return _name;
            }
        }
    }
    
    public long Size => _info.ByteSize;

    public long PathId => _info.PathId;

    public string Container
    {
        get
        {
            var containers = SourceFile.Containers;
            if (containers.TryGetValue(PathId, out var container))
            {
                return container;
            }
            return string.Empty;
        }
    }

    public Asset(SerializedFile sf, AssetFileInfo info)
    {
        _info = info;
        _isNamedAsset = info.Type.Nodes.Any(n => n is {Name: "m_Name", Type: "string", Level: 1} );

        SourceFile = sf;
    }

    /*public void UpdateTypeInfo()
    {
        _isNamedAsset = _info.Type.Nodes.Any(n => n is {Name: "m_Name", Type: "string", Level: 1} );
    }*/

    public void Release()
    {
        lock (_lock)
        {
            _value = null;
        }
    }
}