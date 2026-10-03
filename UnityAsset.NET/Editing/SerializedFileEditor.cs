using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET.Editing;

public interface IAssetEdit
{
    long PathId { get; }
    IUnityAsset Apply(IUnityAsset original);
}

public sealed record AssetEdit(long PathId, Func<IUnityAsset, IUnityAsset> Edit) : IAssetEdit
{
    public IUnityAsset Apply(IUnityAsset original) => Edit(original);
}

/// <summary>Edits apply in insertion order; each branch materializes independently.</summary>
public sealed class SerializedFileEditor
{
    private readonly SerializedFile _original;
    private readonly IAssetEdit[] _edits;
    private readonly IUnityAsset[] _added;
    private readonly Lazy<(byte[] Bytes, SerializedFile File)> _result;

    public SerializedFileEditor(SerializedFile original) : this(original, [], []) { }

    private SerializedFileEditor(SerializedFile original, IAssetEdit[] edits, IUnityAsset[] added)
    {
        _original = original ?? throw new ArgumentNullException(nameof(original));
        _edits = edits;
        _added = added;
        _result = new Lazy<(byte[], SerializedFile)>(Build);
    }

    public SerializedFileEditor With(IAssetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!_original.PathToAsset.ContainsKey(edit.PathId))
            throw new ArgumentException($"Unknown PathID {edit.PathId}.", nameof(edit));
        return new SerializedFileEditor(_original, [.. _edits, edit], _added);
    }

    /// <summary>Adds a snapshot using an existing file type; positive PathIDs are allocated monotonically.</summary>
    public SerializedFileEditor Add(IUnityAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var type = UnityAsset.NET.Types.AssetCloner.Origin(asset)
            ?? throw new InvalidOperationException("The asset has no recorded file type. Supply an existing SerializedType explicitly.");
        return Add(asset, type);
    }

    public SerializedFileEditor Add(IUnityAsset asset, SerializedType type)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(type);
        if (!_original.Metadata.Types.Contains(type))
            throw new ArgumentException("The declared type does not belong to this file.", nameof(type));
        var snapshot = asset.Clone();
        UnityAsset.NET.Types.AssetCloner.RecordOrigin(snapshot, type);
        return new SerializedFileEditor(_original, _edits, [.. _added, snapshot]);
    }

    public SerializedFile Materialize() => _result.Value.File;
    internal byte[] Bytes => _result.Value.Bytes;
    public void Write(Stream destination) => AtomicOutput.Write(destination, Bytes);
    public void Write(string path) => AtomicOutput.Write(path, Bytes);

    private (byte[], SerializedFile) Build()
    {
        var reader = _original.ReaderProvider.CreateReader();
        var source = reader.ReadBytes(checked((int)reader.Length));
        var validIds = _original.Assets.Select(a => a.PathId).ToHashSet();
        var nextId = Math.Max(0, validIds.DefaultIfEmpty().Max());
        foreach (var _ in _added) validIds.Add(checked(++nextId));
        var replacements = new Dictionary<long, byte[]>();
        foreach (var group in _edits.GroupBy(edit => edit.PathId))
        {
            var asset = _original.PathToAsset[group.Key];
            // An arbitrary user delegate never receives the session's cached original.
            var originalValue = asset.Value;
            var value = originalValue.Clone();
            foreach (var edit in group)
            {
                value = edit.Apply(value) ?? throw new InvalidOperationException("An edit returned null.");
                if (value.GetType() != originalValue.GetType())
                    throw new InvalidOperationException($"PathID {asset.PathId}: changing the declared type is unsupported.");
            }
            using var payload = new MemoryStream();
            AssetValidation.References(value, _original, validIds);
            var write = value.GetType().GetMethod("Write", [typeof(IWriter)])
                ?? throw new NotSupportedException($"{value.GetType().Name} has no encoder.");
            using (var writer = new AssetWriter(payload, _original, leaveOpen: true))
            {
                write.Invoke(value, [writer]);
                writer.Finish();
            }
            using var baseline = new MemoryStream();
            using (var writer = new AssetWriter(baseline, _original, leaveOpen: true))
            {
                write.Invoke(originalValue, [writer]);
                writer.Finish();
            }
            // Equal values preserve padding and other bytes normalized by a codec.
            if (!payload.GetBuffer().AsSpan(0, checked((int)payload.Length)).SequenceEqual(
                    baseline.GetBuffer().AsSpan(0, checked((int)baseline.Length))))
                replacements.Add(group.Key, payload.ToArray());
        }

        var sameLengths = replacements.All(entry => entry.Value.Length == _original.PathToAsset[entry.Key].Size);
        byte[] bytes;
        if (sameLengths)
        {
            bytes = source;
            foreach (var (pathId, payload) in replacements)
                payload.CopyTo(bytes, checked((int)(_original.Header.DataOffset + _original.PathToAsset[pathId].Info.ByteOffset)));
        }
        else
        {
            using var output = new MemoryStream();
            output.Write(source.AsSpan(0, checked((int)_original.Header.DataOffset)));
            var entries = new List<(Asset Asset, ulong Offset, uint Size)>();
            var sourceCursor = checked((int)_original.Header.DataOffset);
            foreach (var asset in _original.Assets.OrderBy(a => a.Info.ByteOffset))
            {
                var start = checked((int)(_original.Header.DataOffset + asset.Info.ByteOffset));
                if (start < sourceCursor) throw new InvalidDataException("Overlapping asset payloads.");
                output.Write(source.AsSpan(sourceCursor, start - sourceCursor));
                while ((output.Position - (long)_original.Header.DataOffset) % 8 != 0) output.WriteByte(0);
                var offset = checked((ulong)output.Position - _original.Header.DataOffset);
                var payload = replacements.GetValueOrDefault(asset.PathId)
                    ?? source.AsSpan(start, checked((int)asset.Size)).ToArray();
                output.Write(payload);
                entries.Add((asset, offset, checked((uint)payload.Length)));
                sourceCursor = checked(start + (int)asset.Size);
            }
            output.Write(source.AsSpan(sourceCursor));
            bytes = output.ToArray();
            using var patch = new MemoryStream(bytes, writable: true);
            using var patchWriter = new CustomStreamWriter(patch, _original.Header.Endianness, leaveOpen: true);
            IWriter writer = patchWriter;
            foreach (var (asset, offset, size) in entries)
            {
                patchWriter.Finish();
                patch.Position = asset.Info.TableOffset + 8;
                if (_original.Header.Version >= SerializedFileFormatVersion.LargeFilesSupport) writer.WriteUInt64(offset);
                else writer.WriteUInt32(checked((uint)offset));
                writer.WriteUInt32(size);
            }
            writer.Endian = Endianness.BigEndian;
            patchWriter.Finish();
            if (_original.Header.Version >= SerializedFileFormatVersion.LargeFilesSupport)
            {
                patch.Position = 24;
                writer.WriteUInt64(checked((ulong)bytes.Length));
            }
            else
            {
                patch.Position = 4;
                writer.WriteUInt32(checked((uint)bytes.Length));
            }
            patchWriter.Finish();
        }
        var file = SerializedFile.Parse(null, new MemoryFileProvider(bytes), _original.Metadata.UnityVersion);
        foreach (var asset in file.Assets)
            if (_original.PathToAsset[asset.PathId].Context is { } context) asset.AttachContext(context);
        return _added.Length == 0 ? (bytes, file) : Append(file);
    }

    private (byte[], SerializedFile) Append(SerializedFile file)
    {
        var entries = file.Assets.Select(a => new AssetFileInfo(a.PathId, a.Info.ByteOffset, a.Info.ByteSize,
            a.Info.TypeIdOrIndex, a.Info.Type)).ToList();
        var payloads = file.Assets.Select(a => a.DataReader.ReadBytes(checked((int)a.Size))).ToList();
        var contexts = file.Assets.ToDictionary(a => a.PathId, a => a.Context);
        var nextId = Math.Max(0, entries.Select(a => a.PathId).DefaultIfEmpty().Max());
        foreach (var value in _added)
        {
            var candidates = _original.Assets.Where(a => a.Type == value.ClassName && a.Value.GetType() == value.GetType());
            if (UnityAsset.NET.Types.AssetCloner.Origin(value) is { } origin)
                candidates = candidates.Where(a => ReferenceEquals(a.Info.Type, origin));
            if (value is UnityAsset.NET.Types.PreDefined.Types.MonoBehaviour mono)
                candidates = candidates.Where(a => ((UnityAsset.NET.Types.PreDefined.Types.MonoBehaviour)a.Value).TypeTree.Equals(mono.TypeTree));
            var templates = candidates.GroupBy(a => a.Info.TypeIdOrIndex).Select(g => g.First()).ToArray();
            if (templates.Length != 1)
                throw new InvalidOperationException($"Adding {value.ClassName} requires one unambiguous existing file type.");
            var template = templates[0];
            var method = value.GetType().GetMethod("Write", [typeof(IWriter)])
                ?? throw new NotSupportedException($"{value.GetType().Name} has no encoder.");
            using var output = new MemoryStream();
            using (var writer = new AssetWriter(output, file, leaveOpen: true))
            {
                method.Invoke(value, [writer]);
                writer.Finish();
            }
            var payload = output.ToArray();
            var id = checked(++nextId);
            entries.Add(new AssetFileInfo(id, 0, checked((uint)payload.Length), template.Info.TypeIdOrIndex, template.Info.Type));
            contexts.Add(id, template.Context);
            payloads.Add(payload);
        }
        var validIds = entries.Select(entry => entry.PathId).ToHashSet();
        foreach (var value in _added) AssetValidation.References(value, file, validIds);
        ulong offset = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            offset = checked((offset + 7) & ~7UL);
            entries[i].ByteOffset = offset;
            offset = checked(offset + entries[i].ByteSize);
        }
        var metadata = file.Metadata;
        var newMetadata = new SerializedFileMetadata(metadata.UnityVersion, metadata.TargetPlatform, metadata.TypeTreeEnabled,
            metadata.Types, entries.ToArray(), metadata.ScriptTypes, metadata.Externals, metadata.RefTypes, metadata.UserInformation);
        var headerSize = file.Header.Version >= SerializedFileFormatVersion.LargeFilesSupport ? 48 : 20;
        using var metadataStream = new MemoryStream();
        metadataStream.Write(new byte[headerSize]);
        using (var writer = new CustomStreamWriter(metadataStream, file.Header.Endianness, leaveOpen: true))
        {
            newMetadata.Serialize(writer, file.Header.Version);
            writer.Finish();
        }
        var dataOffset = checked(((ulong)metadataStream.Length + 15) & ~15UL);
        var header = new SerializedFileHeader(checked((uint)(metadataStream.Length - headerSize)),
            checked(dataOffset + offset), file.Header.Version, dataOffset, file.Header.Endianness,
            file.Header.Reserved.ToArray(), file.Header.Unknown);
        using var result = new MemoryStream();
        using (var writer = new CustomStreamWriter(result, leaveOpen: true))
        {
            header.Serialize(writer);
            writer.WriteBytes(metadataStream.ToArray().AsSpan(headerSize));
            IWriter output = writer;
            output.WriteBytes(0, dataOffset - (ulong)writer.Position);
            for (var i = 0; i < entries.Count; i++)
            {
                output.WriteBytes(0, dataOffset + entries[i].ByteOffset - (ulong)writer.Position);
                writer.WriteBytes(payloads[i]);
            }
            writer.Finish();
        }
        var bytes = result.ToArray();
        var materialized = SerializedFile.Parse(null, new MemoryFileProvider(bytes), metadata.UnityVersion);
        foreach (var asset in materialized.Assets)
            if (contexts[asset.PathId] is { } context) asset.AttachContext(context);
        return (bytes, materialized);
    }
}
