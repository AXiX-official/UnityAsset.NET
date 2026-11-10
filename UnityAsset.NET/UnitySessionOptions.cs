using UnityAsset.NET.Diagnostics;
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
    /// What a load does with a type whose tree is in neither the file nor the tpk database. Under
    /// <see cref="ErrorPolicy.SkipAndReport"/> the type is left out of the catalog, every asset of it is marked
    /// unavailable (<see cref="Asset.Available"/> is false and <see cref="Asset.UnavailableReason"/> says why), and one
    /// diagnostic per type says so; the rest of the load continues.
    /// </summary>
    public ErrorPolicy MissingType { get; init; } = ErrorPolicy.Throw;

    /// <summary>
    /// What a load does with a container or a metadata region it cannot read at all — a damaged bundle, a truncated
    /// serialized file. Under <see cref="ErrorPolicy.SkipAndReport"/> the file is dropped from the session and reported
    /// as <see cref="LoadProblem.FileUnreadable"/>.
    /// </summary>
    public ErrorPolicy CorruptFile { get; init; } = ErrorPolicy.Throw;

    /// <summary>
    /// What a load does with one asset that fails to materialise while its type was readable. Under
    /// <see cref="ErrorPolicy.SkipAndReport"/> only that asset is marked unavailable, reported as
    /// <see cref="LoadProblem.AssetUnreadable"/>, and everything else in the session keeps working.
    /// </summary>
    public ErrorPolicy CorruptAsset { get; init; } = ErrorPolicy.Throw;

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