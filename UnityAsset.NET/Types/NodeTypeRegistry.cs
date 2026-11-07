#if UNITYASSET_NODEDATA
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.Types.PreDefined;
namespace UnityAsset.NET.Types;
public sealed class TypeRegistryOptions
{
    public bool WriteGeneratedSource { get; init; }
    public string? CacheDirectory { get; init; }
}
public sealed class TypeRegistry : IDisposable
{
    public const string AssemblyNameSpace = "UnityAsset.NET.RuntimeTypes";
    private IReadOnlyDictionary<Hash128, UnityTypeSource> _catalog = new Dictionary<Hash128, UnityTypeSource>();
    private bool _disposed;
    public TypeRegistry(TypeRegistryOptions? options = null) { }
    public string? ArtifactDirectory => null;
    public string? GeneratedSourcePath => null;
    public void LoadTypes(IReadOnlyDictionary<Hash128, UnityTypeSource> typesToGenerate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(typesToGenerate);
        _catalog = new Dictionary<Hash128, UnityTypeSource>(typesToGenerate);
    }
    public Func<IReader, object> GetFactory(SerializedType type)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(type);
        if (PreDefinedTypeTable.ByUnityTypeName.TryGetValue(type.ToTypeName(), out var predefined)) return predefined.Create;
        if (!_catalog.TryGetValue(type.TypeHash, out var source)) throw new NotSupportedException($"Type {type.Describe()} is not in this session's catalog.");
        return reader =>
        {
            var result = (NodeObject)NodeAdapters.Create(source.ClassName, new NodeData(reader, source.TypeTree).Value, source.TypeTree, reader);
            result.ClassName = source.ClassName;
            return result;
        };
    }
    public void Dispose() { _disposed = true; _catalog = new Dictionary<Hash128, UnityTypeSource>(); }
}
#endif
