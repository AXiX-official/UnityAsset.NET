namespace UnityAsset.NET.Editing;

internal static class AtomicOutput
{
    public static void Write(Stream destination, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite || !destination.CanSeek)
            throw new NotSupportedException("Transactional stream output requires a writable, seekable stream. Use the path overload for atomic file replacement.");
        var position = destination.Position;
        var length = destination.Length;
        var remaining = Math.Max(0, length - position);
        if (remaining > 0 && !destination.CanRead)
            throw new NotSupportedException("Overwriting stream content requires read access for rollback.");
        var previous = new byte[checked((int)remaining)];
        try
        {
            if (previous.Length > 0) destination.ReadExactly(previous);
        }
        finally { destination.Position = position; }
        try { destination.Write(bytes); }
        catch
        {
            destination.Position = position;
            if (previous.Length > 0) destination.Write(previous);
            destination.SetLength(length);
            destination.Position = position;
            throw;
        }
    }

    public static void Write(string path, byte[] bytes)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
