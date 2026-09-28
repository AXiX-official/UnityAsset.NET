using UnityAsset.NET.Types;

namespace UnityAsset.NET;

/// <summary>
/// Per-session options. Everything here used to be a process-wide <see cref="Setting"/> value read from wherever it
/// was needed; two sessions loading different games need different values, so a session carries its own copy.
/// <para>
/// The <see cref="Setting"/> fields stay as the process default: an option left null falls back to them, so a caller
/// that loads a single game does not have to configure anything.
/// </para>
/// <para>
/// There is deliberately no option that limits how many sessions a process may have. Sessions are isolated by design —
/// a session owns its files, catalog, compiled assembly and block cache, and shares only content-addressed state — so
/// "only one session" is an application's decision to make, not a library's to enforce.
/// </para>
/// </summary>
public sealed class UnitySessionOptions
{
    /// <summary>
    /// UnityCN decryption key. Falls back to <see cref="Setting.DefaultUnityCNKey"/> when null, and the key can still
    /// be passed per file through the <c>BundleFile</c> constructors.
    /// </summary>
    public string? UnityCnKey { get; init; }

    /// <summary>
    /// Revision to assume for a file that declares none (a stripped revision, for example). Falls back to
    /// <see cref="Setting.DefaultUnityVerion"/> when null.
    /// </summary>
    public string? DefaultUnityVersion { get; init; }

    /// <summary>
    /// Type tree database used to resolve stripped type trees. Falls back to
    /// <see cref="Setting.DefaultTpkFilePath"/> when null.
    /// </summary>
    public string? TpkFilePath { get; init; }

    /// <summary>Code generation options for this session's type catalog.</summary>
    public TypeRegistryOptions Codegen { get; init; } = new();

    /// <summary>
    /// Budget for this session's decompressed block cache, in bytes. When null the session keeps the loader's
    /// long-standing behaviour: it starts from <see cref="Setting.DefaultBlockCacheSize"/> and switches to three
    /// quarters of the corpus it just registered once a load finishes. An explicit budget is used from the start and
    /// the cache is not rebuilt at the end of a load, so the blocks read while parsing metadata survive.
    /// </summary>
    public long? BlockCacheSize { get; init; }

    /// <summary>The defaults a session uses when no options are given.</summary>
    public static UnitySessionOptions Default { get; } = new();
}