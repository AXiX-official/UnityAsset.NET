using UnityAsset.NET.Enums;
using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFileInfo : IVirtualFileInfo, IDisposable
{
    private readonly Lazy<SafeFileHandle> _handle;

    public DirectFileInfo(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Length = new FileInfo(path).Length;

        // Opened on first use and kept from then on
        _handle = new Lazy<SafeFileHandle>(
            () => File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            LazyThreadSafetyMode.ExecutionAndPublication);

        var probe = Sniff(path, Length);
        FileType = probe.Type;
        UnrecognisedReason = probe.Reason;
    }

    /// <summary>The read handle, opened when something first asks for it.</summary>
    public SafeFileHandle Handle => _handle.Value;

    public string Path { get; }
    public string Name { get; }
    public long Length { get; }
    public FileType FileType { get; set; }

    /// <summary>Why the probe could not name this file, or null when it did.</summary>
    public string? UnrecognisedReason { get; }

    public IVirtualFile GetFile() => new DirectFile(Handle, 0, Length);

    /// <summary>Closes the handle if it was ever opened. A file that was only listed holds nothing.</summary>
    public void Dispose()
    {
        if (_handle.IsValueCreated)
            _handle.Value.Dispose();
    }

    /// <summary>
    /// Recognises the file through a handle that does not outlive the call. The type needs the first 48 bytes and nothing
    /// else, so sniffing through a temporary handle keeps the long-lived one for the files that are actually read.
    /// </summary>
    private static FileProbe Sniff(string path, long length)
    {
        var size = (int)Math.Min(FileTypeHelper.HeadSize, length);
        if (size <= 0)
            return new FileProbe(FileType.Unknown, "the file is empty");

        var head = new byte[size];
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var read = 0;
        while (read < size)
        {
            var count = RandomAccess.Read(handle, head.AsSpan(read), read);
            if (count == 0)
                break;

            read += count;
        }

        return FileTypeHelper.Probe(head.AsSpan(0, read), length);
    }
}