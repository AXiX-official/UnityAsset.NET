using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree;
using UnityAsset.NET.TypeTree.PreDefined;

namespace UnityAsset.NET;

public class Asset : IEquatable<Asset>
{
    public readonly AssetFileInfo Info;
    private WeakReference<IUnityAsset>? _value;
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

    private IUnityAsset GetValue()
    {
        var value = UnityObjectFactory.Create(Info.Type, DataReader);
        BlockReader.OnAssetParsed(this);
        return value;
    }

    
    public IUnityAsset Value
    {
        get
        {
            lock (_lock)
            {
                if (_value is null)
                {
                    var value = GetValue();
                    _value = new WeakReference<IUnityAsset>(value);
                    if (IsNamedAsset)
                    {
                        _name = ((INamedObject)value).m_Name;
                    }

                    return value;
                }
                else if (_value.TryGetTarget(out var value))
                {
                    return  value;
                }

                var newValue = GetValue();
                _value = new WeakReference<IUnityAsset>(newValue);
                return newValue;
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
        IsNamedAsset = info.Type.IsNamed;

        SourceFile = sf;
    }
    
    
    public bool Equals(Asset? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return ReferenceEquals(SourceFile, other.SourceFile)
               && PathId == other.PathId;
    }

    public override bool Equals(object? obj)
        => obj is Asset other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            SourceFile,
            PathId);
    }
}