using UnityAsset.NET.Edit;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

public class EditedAsset : IAsset
{
    private IUnityAsset? _value;

    public IUnityAsset Value => _value ??= ParseSource();
    public string TypeName { get; }
    public string Name { get; set; }

    private readonly long _originalSize;
    public long Size => RawData.HasValue
        ? RawData.Value.Length
        : _value is null
            ? _originalSize
            : Measure(_value);
    
    private long Measure(IUnityAsset value)
    {
        var counter = new CountWriter(_endianness);
        value.Write(new AssetWriterProxy(counter, _sourceFile));
        return counter.Length;
    }

    public long PathId { get; set; }
    public string Container { get; set; }
    public SerializedType Type { get; }
    public Memory<byte>? RawData { get; set; }

    private AssetContext? _context;
    private readonly SerializedFile _sourceFile;
    private readonly Endianness _endianness;
    private readonly AssetReader _source;
    public bool IsRemoved { get; private set; }

    public EditedAsset(IUnityAsset? value, string type, string name, long size, long pathId, string container,
        AssetContext? context, SerializedType typeInfo, AssetReader dataReader)
    {
        _value = value;
        TypeName = type;
        Name = name;
        _originalSize = size;
        PathId = pathId;
        Container = container;

        _context = context;
        Type = typeInfo;
        _sourceFile = dataReader.AssetsFile;
        _endianness = dataReader.Endian;
        _source = dataReader;
    }

    public void Write(IWriter writer)
    {
        if (RawData.HasValue)
        {
            writer.WriteBytes(RawData.Value.Span);
            return;
        }

        if (_value is null)
        {
            _source.Position = 0;
            writer.WriteBytes(_source);
            return;
        }

        _value.Write(writer as IAssetWriter ?? new AssetWriterProxy(writer, _sourceFile));
    }

    private IUnityAsset ParseSource()
    {
        if (RawData.HasValue)
            return Decode(RawData.Value);

        var context = Volatile.Read(ref _context) ??
                      throw new InvalidOperationException("The edited asset has no session attached.");

        _source.Position = 0;
        return context.Factory.Invoke(Type, _source);
    }

    private IUnityAsset Decode(Memory<byte> raw)
    {
        var context = Volatile.Read(ref _context) ?? throw new InvalidOperationException();

        var readerProvider = new MemoryReaderProvider(raw);
        var assetReader = new AssetReader(readerProvider, 0, (ulong)raw.Length, _sourceFile, _endianness);
        return context.Factory.Invoke(Type, assetReader);
    }

    public void Edit(IAssetEdit edit)
    {
        switch (edit)
        {
            case IAssetSetEdit setEdit:
                RawData = null;
                IsRemoved = false;
                _value = setEdit.Value;
                break;
            case IAssetModifyEdit editEdit:
                if (RawData.HasValue)
                {
                    _value = Decode(RawData.Value);
                    RawData = null;
                }
                if (IsRemoved)
                {
                    throw new InvalidOperationException("Cannot edit a removed asset.");
                }
                editEdit.Apply(Value);
                break;
            case AssetSetRaw setRawEdit:
                IsRemoved = false;
                _value = null;
                RawData = setRawEdit.Payload;
                break;
            case AssetRemove:
                IsRemoved = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit));
        }
    }
}