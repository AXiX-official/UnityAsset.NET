using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

internal sealed class AssetContext
{
    public required Func<SerializedType, IReader, IUnityAsset> Factory { get; init; }

    public Action<Asset>? OnParsed { get; init; }
}