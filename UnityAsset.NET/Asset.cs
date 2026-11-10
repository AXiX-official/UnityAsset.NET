using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

public class Asset : IEquatable<Asset>, IAsset
{
    public readonly AssetFileInfo Info;
    private WeakReference<IUnityAsset>? _value;
    private string? _name;
    private readonly Lock _lock = new();
    private AssetContext? _context;
    private AssetUnavailableException? _unavailable;

    public bool IsNamedAsset;

    internal void AttachContext(AssetContext context) => Volatile.Write(ref _context, context);

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
            $"Asset {TypeName}/{PathId} has no session attached: its SerializedFile was not loaded through a session " +
            "(AssetManager), or that session has been cleared or disposed.");

        IUnityAsset value;
        try
        {
            value = context.Factory.Invoke(Info.Type, DataReader);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The session decides: no handler, or a handler that returns null, means "this failure is fatal for the load"
            // and the original exception is rethrown untouched. A handler that returns an exception means "record it and
            // mark this asset unavailable". Out of memory never reaches here: an allocation failure says nothing about
            // this asset, and continuing after it is not something to promise.
            if (context.OnFailed is not { } onFailed)
                throw;

            if (onFailed(this, exception) is not { } unavailable)
                throw;

            _unavailable = unavailable;
            throw unavailable;
        }

        // Only after a successful materialisation: the session uses this to account for the blocks it just read.
        context.OnParsed?.Invoke(this);

        return value;
    }

    /// <summary>
    /// Whether this asset can be read. True until a failure is recorded, so the value has to be asked for before an
    /// unreadable asset reports itself as such — "not parsed yet" and "cannot be parsed" are different claims.
    /// </summary>
    public bool Available => _unavailable is null;

    /// <summary>Why the asset is unavailable, or null while it is available.</summary>
    public string? UnavailableReason => _unavailable?.Reason;

    /// <summary>
    /// Marks the asset unreadable without trying to materialise it. Used for an asset whose type the session already
    /// knows it cannot read, so the failure is recorded once for the type instead of once per asset.
    /// </summary>
    internal void MarkUnavailable(AssetUnavailableException unavailable) => _unavailable ??= unavailable;

    public IUnityAsset Value
    {
        get
        {
            lock (_lock)
            {
                if (_unavailable is { } unavailable)
                    throw unavailable;

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

    public string TypeName => Info.Type.ToTypeName();
    
    public SerializedType Type => Info.Type;

    public string Name
    {
        get
        {
            lock (_lock)
            {
                // An unreadable asset has no name to report, and returning "" would read as "this class is not named".
                if (_unavailable is { } unavailable)
                    throw unavailable;

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

    public EditedAsset ToEditedAsset()
    {
        return new EditedAsset(null, TypeName, Name, Size, PathId, Container, _context, Info.Type, DataReader);
    }

    public void Write(IWriter writer)
    {
        writer.WriteBytes(DataReader);
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
                $"Asset {TypeName}/{PathId} is named but its generated type " +
                $"{value.GetType().Name} does not implement INamedObject.");
}