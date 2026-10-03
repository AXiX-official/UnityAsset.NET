using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

public class Asset : IEquatable<Asset>
{
    public readonly AssetFileInfo Info;
    private WeakReference<IUnityAsset>? _value;
    private string? _name;
    private readonly Lock _lock = new();
    private AssetContext? _context;

    public bool IsNamedAsset;

    internal void AttachContext(AssetContext context) => Volatile.Write(ref _context, context);
    internal AssetContext? Context => Volatile.Read(ref _context);

    internal void DetachContext() => Volatile.Write(ref _context, null);

    public AssetReader DataReader
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
        var context = Volatile.Read(ref _context) ?? throw new InvalidOperationException(
            $"Asset {Type}/{PathId} has no session attached: its SerializedFile was not loaded through a session " +
            "(AssetManager), or that session has been cleared or disposed.");

        var value = context.Factory.Invoke(Info.Type, DataReader);
        UnityAsset.NET.Types.AssetCloner.RecordOrigin(value, Info.Type);

        // Only after a successful materialisation: the session uses this to account for the blocks it just read.
        context.OnParsed?.Invoke(this);

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
                        _name = ReadName(value);
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

                _name ??= ReadName(Value);

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
    
    private string ReadName(IUnityAsset value)
        => value is INamedObject named
            ? named.m_Name
            : throw new InvalidOperationException(
                $"Asset {Type}/{PathId} is named but its generated type " +
                $"{value.GetType().Name} does not implement INamedObject.");
}
