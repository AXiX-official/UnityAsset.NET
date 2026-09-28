namespace UnityAsset.NET.Tests.Support;

/// <summary>
/// A unique directory that is removed when the test finishes. Tests that exercise artifact writing must never
/// share a directory with another test, otherwise the "one directory per compilation" assertions become flaky.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "UnityAssetNET.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Names of the directories currently inside this one (sorted, so assertions are stable).</summary>
    public string[] SubDirectoryNames()
        => Directory.Exists(Path)
            ? Directory.GetDirectories(Path)
                .Select(directory => System.IO.Path.GetFileName(directory))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()!
            : [];

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must never fail a test run.
        }
    }
}