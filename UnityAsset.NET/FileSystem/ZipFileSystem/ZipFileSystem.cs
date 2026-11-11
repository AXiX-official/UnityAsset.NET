using System.IO.Compression;

namespace UnityAsset.NET.FileSystem.ZipFileSystem;

/// <summary>
/// A load that understands archives: an entry of a zip or an apk becomes a file, a path that is not an archive goes to
/// the wrapped file system, and the whole result is then handed to <see cref="Transforms"/> in order.
/// <para>
/// The archive layer is deliberately the first stage and not the last one. An archive holds files, so everything after it
/// has to think in files; a game format that has to be decoded — a container split into entries, an encrypted block
/// table — is then also a stage over files, and it no longer matters whether its input arrived loose on disk or as an
/// entry of an apk. The other composition, a game file system wrapping this one, cannot work: a stage that only accepts
/// paths can never be shown an entry.
/// </para>
/// </summary>
public sealed class ZipFileSystem : IFileSystem
{
    public static readonly string ArchiveSeparator = "|";
    private readonly IFileSystem _source;

    /// <summary>
    /// Entries of the current load, including the ones a later stage consumed and that are therefore no longer in
    /// <see cref="LoadedFiles"/>. They still own a read handle and, above the spill threshold, a file on disk, so the
    /// file system has to remember them to release them.
    /// </summary>
    private readonly List<ZipFileInfo> _entries = new();

    public ZipFileSystem(IFileSystem? source = null, IReadOnlyList<IVirtualFileTransform>? transforms = null)
    {
        _source = source ?? new DirectFileSystem.DirectFileSystem();
        Transforms = transforms ?? Array.Empty<IVirtualFileTransform>();
    }
    

    public IFileSystem.ErrorHandler? OnError { get; set; }

    /// <summary>The stages this load runs once its archives are open, in order; see <see cref="IVirtualFileTransform"/>.</summary>
    public IReadOnlyList<IVirtualFileTransform> Transforms { get; }

    /// <summary>
    /// Entries above this many bytes are inflated to a temp file instead of a buffer. Starts at
    /// <see cref="Setting.ZipSpillThreshold"/> and is per instance, so two sessions can disagree without touching a
    /// process-wide value.
    /// </summary>
    public long SpillThreshold { get; set; } = Setting.ZipSpillThreshold;

    /// <summary>
    /// Where an entry above <see cref="SpillThreshold"/> is written. Starts at
    /// <see cref="Setting.ZipSpillDirectory"/> and is per instance for the same reason the threshold is; the directory is
    /// created on first use, and an empty value falls back to the process default rather than failing a load.
    /// </summary>
    public string SpillDirectory { get; set; } = Setting.ZipSpillDirectory;

    public List<IVirtualFileInfo> LoadedFiles { get; private set; } = new();
    
    private readonly Dictionary<string, HashSet<string>> _archiveEntryNames = new(StringComparer.Ordinal);

    public async Task<List<IVirtualFileInfo>> LoadAsync(List<string> paths, IProgress<LoadProgress>? progress = null)
    {
        var files = new List<IVirtualFileInfo>();
        var loose = new List<string>();

        // A load replaces the previous one, so whatever it left on disk goes first. A load that fails is released on the
        // way out, because its entries are not in LoadedFiles yet and nothing else would ever release them.
        ReleaseEntries();
        LoadedFiles = new List<IVirtualFileInfo>();

        try
        {
            await Task.Run(() =>
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    var path = paths[index];
                    try
                    {
                        if (IsArchive(path))
                        {
                            _archiveEntryNames[path] = new ();
                            Expand(path, files, progress);
                        }
                        else
                            loose.Add(path);
                    }
                    catch (Exception exception)
                    {
                        // A damaged archive is one input, not the session: report it and keep the rest.
                        OnError?.Invoke(path, exception, $"Fail to read archive {path}: {exception.Message}");
                    }

                    progress?.Report(new LoadProgress(
                        $"ZipFileSystem: {Path.GetFileName(path)}", paths.Count, index + 1));
                }
            });

            if (loose.Count > 0)
            {
                _source.OnError = OnError;
                files.AddRange(await _source.LoadAsync(loose, progress));
            }

            files = await RunTransformsAsync(files, progress);
        }
        catch
        {
            ReleaseEntries();
            throw;
        }

        LoadedFiles = files;
        return files;
    }

    public void Clear()
    {
        ReleaseEntries();
        LoadedFiles = new List<IVirtualFileInfo>();
        _source.Clear();
    }

    /// <summary>Runs the stages in order; a stage that throws is reported and skipped, leaving its inputs untouched.</summary>
    private async Task<List<IVirtualFileInfo>> RunTransformsAsync(List<IVirtualFileInfo> files,
        IProgress<LoadProgress>? progress)
    {
        foreach (var transform in Transforms)
        {
            if (files.Count == 0)
                break;

            transform.OnError = OnError;
            try
            {
                files = await transform.TransformAsync(files, progress);
            }
            catch (Exception exception)
            {
                OnError?.Invoke(transform.Name, exception,
                    $"Fail to run {transform.Name}: {exception.Message}");
            }
        }

        return files;
    }

    private static bool IsArchive(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> head = stackalloc byte[4];
            if (stream.Read(head) < 4)
                return false;

            // PK\3\4 (a normal archive), PK\5\6 (an empty one), PK\7\8 (spanned).
            return head[0] == (byte)'P' && head[1] == (byte)'K' && head[2] is 3 or 5 or 7;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Reads one entry's payload and decides where it lives. This runs on the first <see cref="ZipFileInfo.GetFile"/>, not
    /// while the archive is being listed, so an entry nothing reads is never inflated — which is what lets an apk be
    /// listed in full without paying for its resources and code.
    /// </summary>
    private ZipEntryStorage Materialise(ZipFileInfo info)
    {
        try
        {
            // The archive was opened and closed while it was listed; the entry is looked up again here, which costs a
            // central-directory parse and nothing more.
            using var archive = ZipFile.OpenRead(info.ArchivePath);
            var entry = archive.GetEntry(info.EntryPath)
                        ?? throw new FileNotFoundException(
                            $"Entry {info.EntryPath} is no longer in {info.ArchivePath}.");

            // The instance value, not the process one: a caller that moved the threshold did so to change where this
            // load's bytes go, and reading the global here would make the per-instance property a lie.
            if (entry.Length <= SpillThreshold)
                return ZipEntryStorage.InMemory(ReadEntry(entry));

            var spillPath = Spill(entry);
            try
            {
                return ZipEntryStorage.OnDisk(
                    File.OpenHandle(spillPath, FileMode.Open, FileAccess.Read, FileShare.Read), entry.Length, spillPath);
            }
            catch
            {
                // Opening what was just written cannot leave the file behind if it fails.
                TryDelete(spillPath);
                throw;
            }
        }
        catch (Exception exception)
        {
            // A payload that cannot be read is reported where every other file problem is, and then thrown: the caller
            // asked for bytes that do not exist.
            OnError?.Invoke(info.Path, exception, $"Fail to read {info.EntryPath}: {exception.Message}");
            throw;
        }
    }

    private void Expand(string archivePath, List<IVirtualFileInfo> into, IProgress<LoadProgress>? progress)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Where(entry => !IsDirectory(entry)).ToList();

        var archiveEntries = _archiveEntryNames[archivePath];
        
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            archiveEntries.Add(entry.Name);
            progress?.Report(new LoadProgress($"ZipFileSystem: {entry.FullName}", entries.Count, index + 1));

            // One partially read stream decides the type; nothing else is read. The payload waits for the first open, so
            // an entry nobody asks for costs 48 bytes of inflate and no memory.
            var probe = FileTypeHelper.Probe(ReadHead(entry), entry.Length);

            var file = new ZipFileInfo(archivePath, entry.FullName, entry.Length, probe.Type, Materialise, probe.Reason);

            _entries.Add(file);
            into.Add(file);
        }
    }

    /// <summary>A directory entry carries no content and no name worth reporting.</summary>
    private static bool IsDirectory(ZipArchiveEntry entry)
        => entry.FullName.EndsWith('/') || entry.Name.Length == 0;

    /// <summary>At most <see cref="FileTypeHelper.HeadSize"/> bytes of an entry, enough for the probe and nothing more.</summary>
    private static byte[] ReadHead(ZipArchiveEntry entry)
    {
        var length = (int)Math.Min(FileTypeHelper.HeadSize, entry.Length);
        if (length == 0)
            return [];

        var head = new byte[length];
        using var stream = entry.Open();

        var read = 0;
        while (read < length)
        {
            var count = stream.Read(head, read, length - read);
            if (count == 0)
                break;

            read += count;
        }

        return read == length ? head : head[..read];
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        var length = checked((int)entry.Length);
        var data = new byte[length];
        using var stream = entry.Open();

        var read = 0;
        while (read < length)
        {
            var count = stream.Read(data, read, length - read);
            if (count == 0)
                break;

            read += count;
        }

        // A central directory that disagrees with the payload is a damaged archive, not something to pad.
        return read == length ? data : data[..read];
    }

    /// <summary>Inflates an entry into the spill directory, because it is too large to keep in a buffer.</summary>
    private string Spill(ZipArchiveEntry entry)
    {
        // An empty value falls back instead of throwing: a settable string is one keystroke away from being blank, and a
        // load failing over where a temp file would go is a worse outcome than using the default directory.
        var directory = string.IsNullOrWhiteSpace(SpillDirectory) ? Setting.ZipSpillDirectory : SpillDirectory;
        Directory.CreateDirectory(directory);

        var spillPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".bin");
        using var source = entry.Open();
        using var target = File.Create(spillPath);
        source.CopyTo(target);
        return spillPath;
    }

    /// <summary>Releases every entry of the current load, including the ones a stage consumed and anything spilled.</summary>
    private void ReleaseEntries()
    {
        foreach (var entry in _entries)
        {
            // The handle has to go before the file can be deleted on Windows, so this order is not cosmetic.
            entry.Dispose();

            if (entry.SpillPath is { } spillPath)
                TryDelete(spillPath);
        }

        _entries.Clear();
    }

    /// <summary>
    /// Deletes a spilled payload. A temp file that will not delete is reported rather than hidden: it is the caller's
    /// disk, and a temp directory quietly filling up is the kind of failure only noticed when it is too late.
    /// </summary>
    private void TryDelete(string spillPath)
    {
        try
        {
            File.Delete(spillPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            OnError?.Invoke(spillPath, exception, $"Fail to delete spilled entry {spillPath}: {exception.Message}");
        }
    }
    
    public List<IVirtualFileInfo> LoadFile(List<string> paths)
    {
        var directFiles = paths.Where(p => !p.Contains(ArchiveSeparator)).ToList();
        var archiveFiles = paths.Where(p => p.Contains(ArchiveSeparator)).ToList();
        
        var addedFiles = _source.LoadFile(directFiles);
        
        foreach (var group in archiveFiles
                     .Select(p => p.Split(ArchiveSeparator, 2))
                     .Where(parts => parts.Length == 2)
                     .GroupBy(parts => parts[0], parts => parts[1], StringComparer.Ordinal))
        {
            using var archive = ZipFile.OpenRead(group.Key);
            foreach (var entryName in group)
            {
                var entry = archive.GetEntry(entryName);
                if (entry is null) continue;
                var probe = FileTypeHelper.Probe(ReadHead(entry), entry.Length);
                addedFiles.Add(new ZipFileInfo(group.Key, entry.FullName, entry.Length, probe.Type, Materialise, probe.Reason));
            }
        }

        return addedFiles;
    }
}