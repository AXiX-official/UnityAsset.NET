using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET.Edit;

public interface IAssetEdit
{
    IAssetEdit Fork();
}

public interface IAssetSetEdit : IAssetEdit
{
    IUnityAsset Value { get; }
}

public interface IAssetModifyEdit : IAssetEdit
{
    void Apply(IUnityAsset value);
}

public record AssetSet<T>(T Value) : IAssetSetEdit where T : class, IUnityAsset
{
    IUnityAsset IAssetSetEdit.Value => Value;

    public IAssetEdit Fork() => this with { };
}

public record AssetEdit<T>(Action<T> Edit) : IAssetModifyEdit where T : class, IUnityAsset
{
    public void Apply(IUnityAsset value) => Edit((T)value);

    public IAssetEdit Fork() => this with { };
}

public record AssetSetRaw(Memory<byte> Payload) : IAssetEdit
{
    public IAssetEdit Fork() => this with { };
}

public record AssetRemove : IAssetEdit
{
    public IAssetEdit Fork() => this with { };
}

public class FileEdit
{
    public SerializedFile File;
    
    private Dictionary<long, List<IAssetEdit>> _edits = new();

    public FileEdit(SerializedFile file)
    {
        File = file;
    }
    
    private void NewEdit(long pathId, IAssetEdit edit)
    {
        if (!_edits.TryGetValue(pathId, out var edits))
        {
            edits = new List<IAssetEdit>();
            _edits[pathId] = edits;
        }
        edits.Add(edit);
    }

    public void Edit<T>(Asset asset, Action<T> edit) where T : class, IUnityAsset
    {
        NewEdit(asset.PathId, new AssetEdit<T>(edit));
    }

    public void Edit<T>(long pathId, Action<T> edit) where T : class, IUnityAsset
    {
        NewEdit(pathId, new AssetEdit<T>(edit));
    }

    public void Set<T>(Asset asset, T value) where T : class, IUnityAsset
    {
        NewEdit(asset.PathId, new AssetSet<T>(value));
    }
    
    public void Set<T>(long pathId, T value) where T : class, IUnityAsset
    {
        NewEdit(pathId, new AssetSet<T>(value));
    }
    
    public void Set(Asset asset, Memory<byte> payload)
    {
        NewEdit(asset.PathId, new AssetSetRaw(payload));
    }
    
    public void Set(long pathId, Memory<byte> payload)
    {
        NewEdit(pathId, new AssetSetRaw(payload));
    }
    
   /* public void Add<T>(long pathId, T value) where T : class, IUnityAsset
    {
        NewEdit(pathId, new AssetSet<T>(value));
    }
    
    public void Add(long pathId, Memory<byte> payload)
    {
        NewEdit(pathId, new AssetSetRaw(payload));
    }*/
    
    public void Remove(Asset asset)
    {
        NewEdit(asset.PathId, new AssetRemove());
    }

    public void Remove(long pathId)
    {
        NewEdit(pathId, new AssetRemove());
    }

    public bool Revert(Asset asset)
    {
        return _edits.Remove(asset.PathId);
    }
    
    public bool Revert(long pathId)
    {
        return _edits.Remove(pathId);
    }

    public FileEdit Fork()
    {
        return new FileEdit(File)
        {
            _edits = _edits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(e => e.Fork()).ToList()
            )
        };
    }

    public bool IsChanged()
    {
        bool changed = false;
        foreach (var kvp in _edits)
        {
            if (kvp.Value.Count > 0)
            {
                changed = true;
                break;
            }
        }
        return changed;
    }
    
    public bool IsChanged(long pathId)
    {
        return _edits.TryGetValue(pathId, out var edits) && edits.Count > 0;
    }

    public void Discard()
    {
        _edits.Clear();
    }

    public ulong WriteBytes(IWriter writer)
    {
        var assets = new List<IAsset>(File.Assets.Count);

        foreach (var asset in File.Assets)
        {
            if (!_edits.TryGetValue(asset.PathId, out var edits) || edits.Count == 0)
            {
                // Untouched assets are still part of the file: the object table is rebuilt from this list, so
                // leaving them out would drop them from the written file.
                assets.Add(asset);
                continue;
            }

            var editAsset = asset.ToEditedAsset();
            foreach (var edit in edits)
            {
                editAsset.Edit(edit);
            }
            if (!editAsset.IsRemoved)
                assets.Add(editAsset);
        }
        
        var pos = writer.Position;
        File.Write(writer, assets);
        return (ulong)(writer.Position - pos);
    }
}

public class EditedSerializedFile : IFile
{
    private readonly FileEdit _fileEdit;
    private readonly SerializedFile _serializedFile;

    public EditedSerializedFile(FileEdit fileEdit)
    {
        _fileEdit = fileEdit;
        _serializedFile = fileEdit.File;
    }

    public ulong WriteBytes(IWriter writer)
    {
        return _fileEdit.WriteBytes(writer);
    }
}