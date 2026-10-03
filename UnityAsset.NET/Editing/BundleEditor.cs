using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;

namespace UnityAsset.NET.Editing;

public sealed class BundleEditor
{
    private readonly BundleFile _original;
    private readonly Dictionary<string, SerializedFileEditor> _files;
    private readonly Lazy<(byte[] Bytes, BundleFile File)> _result;

    public BundleEditor(BundleFile original) : this(original, new(StringComparer.Ordinal)) { }
    private BundleEditor(BundleFile original, Dictionary<string, SerializedFileEditor> files)
    {
        _original = original ?? throw new ArgumentNullException(nameof(original));
        _files = files;
        _result = new(Build);
    }

    public BundleEditor WithFile(string path, SerializedFileEditor file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!_original.Files.Any(f => f.Info.Path == path)) throw new ArgumentException($"Unknown bundle path: {path}");
        var files = new Dictionary<string, SerializedFileEditor>(_files, StringComparer.Ordinal) { [path] = file };
        return new BundleEditor(_original, files);
    }

    public BundleFile Materialize() => _result.Value.File;
    public void Write(Stream destination) => AtomicOutput.Write(destination, _result.Value.Bytes);
    public void Write(string path) => AtomicOutput.Write(path, _result.Value.Bytes);

    private (byte[], BundleFile) Build()
    {
        var source = _original.SourceVirtualFile ?? throw new InvalidOperationException("The original bundle must have source bytes.");
        var reader = new CustomFileReaderProvider(source).CreateReader();
        var originalBytes = reader.ReadBytes(checked((int)reader.Length));
        var unchanged = true;
        var files = new List<FileWrapper>();
        foreach (var entry in _original.Files)
        {
            byte[] bytes;
            var raw = entry.File is SerializedFile sf ? sf.ReaderProvider.CreateReader()
                : entry.File is IReaderProvider provider ? provider.CreateReader() : null;
            if (raw == null) throw new NotSupportedException($"No source bytes for {entry.Info.Path}.");
            var original = raw.ReadBytes(checked((int)raw.Length));
            if (_files.TryGetValue(entry.Info.Path, out var editor))
            {
                bytes = editor.Bytes;
                unchanged &= original.AsSpan().SequenceEqual(bytes);
            }
            else bytes = original;
            files.Add(new FileWrapper(new MemoryFileProvider(bytes), entry.Info));
        }
        byte[] result;
        if (unchanged) result = originalBytes;
        else
        {
            var bundle = new BundleFile(_original.Header!, _original.DataInfo!, files,
                _original.UnityCnKey, unityCnInfo: _original.UnityCnInfo);
            using var output = new MemoryStream();
            using (var writer = new CustomStreamWriter(output, leaveOpen: true))
            {
                bundle.Serialize(writer, CompressionType.Lz4HC, CompressionType.Lz4HC, _original.UnityCnKey);
                writer.Finish();
            }
            result = output.ToArray();
        }
        var materialized = new BundleFile(new MemoryFileInfo(source.Path, result, FileType.BundleFile), _original.UnityCnKey);
        materialized.ParseFilesWithTypeConversion();
        foreach (var entry in materialized.Files)
        {
            if (entry.File is not SerializedFile sf) continue;
            var template = _files.TryGetValue(entry.Info.Path, out var editor) ? editor.Materialize()
                : _original.Files.First(e => e.Info.Path == entry.Info.Path).File as SerializedFile;
            foreach (var asset in sf.Assets)
                if (template?.PathToAsset.GetValueOrDefault(asset.PathId)?.Context is { } context) asset.AttachContext(context);
        }
        return (result, materialized);
    }
}
