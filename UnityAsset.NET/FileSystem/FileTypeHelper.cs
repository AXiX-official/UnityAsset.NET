using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.FileSystem;

public static class FileTypeHelper
{
    /// <summary>
    /// Enough bytes to recognise every input and no more: a bundle's magic is 7 bytes, the 64-bit serialized header is 48.
    /// A source of files reads this much to decide what it has, and nothing else until something opens the file.
    /// </summary>
    public const int HeadSize = 48;

    public static FileType GetFileType(IVirtualFileInfo fileInfo) => Probe(fileInfo).Type;

    public static FileProbe Probe(IVirtualFileInfo fileInfo)
    {
        IReader reader = new CustomFileReader(fileInfo);
        var length = checked((int)Math.Min(HeadSize, reader.Length));
        if (length <= 0)
            return new FileProbe(FileType.Unknown, "the file is empty");

        var head = new byte[length];
        reader.ReadExactly(head);
        return Probe(head, reader.Length);
    }

    /// <summary>
    /// Decides what a file is from its first <see cref="HeadSize"/> bytes and its length, and says why when it cannot.
    /// </summary>
    public static FileProbe Probe(ReadOnlySpan<byte> head, long length)
    {
        if (head.Length >= 7 && head[..7].SequenceEqual("UnityFS"u8))
            return FileProbe.Of(FileType.BundleFile);

        if (length < 20 || head.Length < 20)
            return new FileProbe(FileType.Unknown, $"it is {length} bytes and a serialized header needs 20");

        var version = (SerializedFileFormatVersion)System.Buffers.Binary.BinaryPrimitives
            .ReadUInt32BigEndian(head[8..]);

        if (!SerializedFileHeader.TryParse(head, out var header))
            return new FileProbe(FileType.Unknown,
                $"a version {(uint)version} header is {SerializedFileHeader.SizeOf(version)} bytes and only {head.Length} were read");

        var declared = (long)header.FileSize;
        var dataOffset = (long)header.DataOffset;

        if (declared == length && dataOffset <= length)
            return FileProbe.Of(FileType.SerializedFile);

        // A file larger than it declares is a file with something appended — a wrapper's footer, a second file behind
        // this one — and accepting it loses nothing: every read stays inside the sizes the header promised, so the extra
        // bytes are never touched. A file smaller than it declares is truncated, the bytes the header promises are not
        // there, and accepting it only moves the failure to the first read past the end.
        //
        // The margin bounds the largest appended tail worth believing. Without it a small declared size in a huge file
        // would pass, which is what a coincidence looks like. A header that is otherwise consistent gets the benefit of
        // the doubt as well.
        var margin = Math.Max(64, length / 100);
        if (declared < length && length - declared <= margin && IsPlausibleVersion(version) &&
            header.MetadataSize <= length && dataOffset <= length)
            return FileProbe.Of(FileType.SerializedFile);

        // A header that is recognisably one of ours but shorter than it says is a truncated file, and that is a problem
        // to report: silently keeping it as a resource would make "this file could not be read" look like "this file held
        // nothing". The version window is what separates this from garbage whose bytes happen to look like a size.
        if (declared > length && IsPlausibleVersion(version) && header.MetadataSize <= length && dataOffset <= length)
            return new FileProbe(FileType.Unknown,
                $"it declares {declared} bytes and the file is {length}, so it is truncated");

        // Neither a container nor a consistent serialized file. It stays a resource because that is exactly what a
        // streamed resource and an extracted type tree are: files the session keeps and reads on demand, whose bytes
        // never described themselves.
        return new FileProbe(FileType.ResourceFile, $"it declares {declared} bytes and the file is {length}");
    }

    /// <summary>
    /// The window a tolerated match has to stay inside. Without it a 1% coincidence would be enough to call a picture a
    /// serialized file: PNG's width sits where the version is, and 1920 is far outside this window.
    /// </summary>
    private static bool IsPlausibleVersion(SerializedFileFormatVersion version)
        => version >= SerializedFileFormatVersion.RefactorTypeData && (uint)version <= 256;
}
