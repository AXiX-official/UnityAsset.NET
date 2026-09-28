using System.Diagnostics.CodeAnalysis;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET;

public interface IUnitySession
{
    UnityRevision? Version { get; }

    BuildTarget? BuildTarget { get; }

    bool TryGetLoadedFile(string name, [NotNullWhen(true)] out IFile? file);

    byte[]? LoadStreamingData(StreamingInfo streamingInfo);

    /// <summary>The type catalog of this session, keyed by type hash.</summary>
    IReadOnlyDictionary<Hash128, TypeTreeRepr> LoadedTypes { get; }
}
