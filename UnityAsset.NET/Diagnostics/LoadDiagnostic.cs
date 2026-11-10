using System.Collections.Concurrent;

namespace UnityAsset.NET.Diagnostics;

/// <summary>
/// What a loading stage does when it cannot read something.
/// <para>
/// <see cref="Throw"/> is the default and keeps the behaviour every caller has today. <see cref="SkipAndReport"/> marks
/// the item unavailable and records a <see cref="LoadDiagnostic"/> instead of aborting the whole load, so "nothing was
/// extracted" and "there was nothing to extract" stay distinguishable. See
/// <c>docs/adr/0010-loading-leniency-is-a-per-stage-policy-that-reports.md</c>.
/// </para>
/// </summary>
public enum ErrorPolicy
{
    Throw,
    SkipAndReport,
}

/// <summary>
/// What a load could not do with one item. An enum rather than a numeric code, because the meaning has to ship inside
/// the assembly: this repository keeps its documents out of version control, so a <c>LD0003</c> in a log would be a token
/// nobody can look up. The member name is the explanation, and a <c>switch</c> over it is exhaustive.
/// </summary>
public enum LoadProblem
{
    /// <summary>The input was not recognised as a serialized file or a bundle, so nothing was read from it.</summary>
    UnknownFile,

    /// <summary>A file of this name was already loaded, and this copy was dropped.</summary>
    DuplicateFile,

    /// <summary>
    /// The type's tree is in neither the file nor the tpk database, so the type is unreadable and every asset of it is
    /// unavailable. Reported once per type, not once per asset.
    /// </summary>
    MissingType,

    /// <summary>The tree was available but the generator produced no type for it.</summary>
    TypeNotGenerated,

    /// <summary>A container or a metadata region could not be read at all, so the whole file was dropped.</summary>
    FileUnreadable,

    /// <summary>One asset failed to materialise while its type was readable.</summary>
    AssetUnreadable,
}

/// <summary>Where a load diagnostic came from, as far as the stage that produced it can say.</summary>
public sealed record LoadLocation(
    string? File = null,
    string? Container = null,
    string? TypeName = null,
    long? PathId = null)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(File))
            parts.Add($"file {File}");
        if (!string.IsNullOrEmpty(Container))
            parts.Add($"container {Container}");
        if (!string.IsNullOrEmpty(TypeName))
            parts.Add($"type {TypeName}");
        if (PathId is not null)
            parts.Add($"pathID {PathId}");

        return parts.Count == 0 ? "unknown location" : string.Join(", ", parts);
    }
}

/// <summary>One thing a load could not read: what kind of problem, what the stage saw, and where.</summary>
public sealed record LoadDiagnostic(LoadProblem Problem, string Message, LoadLocation Location)
{
    public override string ToString() => $"{Problem}: {Message} ({Location})";
}

/// <summary>
/// Collects what a load could not read, so a session reports every skipped item instead of only the first failure. Safe
/// to write from the parallel file loop: writes are queued and a read gets a snapshot.
/// </summary>
public sealed class LoadDiagnosticBag
{
    private ConcurrentQueue<LoadDiagnostic> _diagnostics = new();

    /// <summary>A snapshot, so a caller can enumerate while another thread is still reporting.</summary>
    public IReadOnlyList<LoadDiagnostic> Diagnostics => _diagnostics.ToArray();

    public int Count => _diagnostics.Count;

    public void Report(LoadDiagnostic diagnostic) => _diagnostics.Enqueue(diagnostic);

    public void Report(LoadProblem problem, string message, LoadLocation location)
        => Report(new LoadDiagnostic(problem, message, location));

    /// <summary>Drops everything reported. Called when a session starts a load or releases its state.</summary>
    public void Clear() => _diagnostics = new ConcurrentQueue<LoadDiagnostic>();
}
