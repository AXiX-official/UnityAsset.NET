using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Stream;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined;

namespace UnityAsset.NET;

public class Asset
{
    public readonly AssetFileInfo Info;
    private IUnityAsset? _value;
    private string? _name;
    private readonly Lock _lock = new();
    public bool IsNamedAsset;

    private AssetReader DataReader
    {
        get
        {
            var sf = SourceFile;
            var readerProvider = sf.ReaderProvider;
            var start = sf.Header.DataOffset + Info.ByteOffset;
            var length = Info.ByteSize;
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
                    using var reader = DataReader;
                    _value = UnityObjectFactory.Create(Info.Type, reader);
                    BlockStream.OnAssetParsed(this);
                    if (IsNamedAsset)
                    {
                        _name = ((INamedObject)_value).m_Name;
                    }
                }
                return _value;
            }
        }
    }

    public string Type => Info.Type.ToTypeName();

    public string Name
    {
        get
        {
            lock (_lock)
            {
                if (!IsNamedAsset)
                    return string.Empty;

                _name ??= ((INamedObject)Value).m_Name;

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
        Info = info;
        IsNamedAsset = info.Type.Nodes.Any(n => n is {Name: "m_Name", Type: "string", Level: 1} );

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