using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET.Types;

public sealed class UnityObjectFactory
{
    private readonly IReadOnlyDictionary<Hash128, UnityTypeSource> _catalog;
    private readonly TypeRegistry _typeRegistry;

    public UnityObjectFactory(IReadOnlyDictionary<Hash128, UnityTypeSource> catalog, TypeRegistry typeRegistry)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(typeRegistry);

        _catalog = catalog;
        _typeRegistry = typeRegistry;
    }

    public IUnityAsset Create(SerializedType sType, IReader reader)
    {
        ArgumentNullException.ThrowIfNull(sType);

        // Script classes carry their own type tree, so they are parsed through the catalog instead of generated code.
        if (sType.ToTypeName() == "MonoBehaviour")
        {
            if (!_catalog.TryGetValue(sType.TypeHash, out var source))
                throw new NotSupportedException(
                    $"MonoBehaviour is not part of this catalog ({sType.Describe()}). The file it belongs to was " +
                    "not loaded through the session that owns this factory.");

            return new PreDefined.Types.MonoBehaviour(reader, source.TypeTree);
        }
        
        var create = _typeRegistry.GetFactory(sType);
        return (IUnityAsset)create(reader);
    }
}