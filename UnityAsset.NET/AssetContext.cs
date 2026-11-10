using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET;

public sealed class AssetContext
{
    public required Func<SerializedType, IReader, IUnityAsset> Factory { get; init; }

    public Action<Asset>? OnParsed { get; init; }

    /// <summary>
    /// Decides what a failed materialisation does. Returning null rethrows the original exception, which is what
    /// <see cref="Diagnostics.ErrorPolicy.Throw"/> means and what every caller gets by default; returning an exception
    /// marks the asset unavailable and rethrows that one instead. The session owns the reporting, because only it knows
    /// which stage policy applies and where the diagnostic goes.
    /// </summary>
    public Func<Asset, Exception, AssetUnavailableException?>? OnFailed { get; init; }
}
