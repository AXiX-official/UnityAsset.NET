using System.Buffers.Binary;
using UnityAsset.NET.Enums;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.FileSystem;

public static class FileTypeHelper
{
    public const int HeadSize = 48;

    public static FileType GetFileType(IVirtualFileInfo fileInfo)
    {
        IReader reader = new CustomFileReader(fileInfo);
        var length = checked((int)Math.Min(HeadSize, reader.Length));
        if (length <= 0)
            return FileType.Unknown;

        var head = new byte[length];
        reader.ReadExactly(head);
        return GetFileType(head, reader.Length);
    }
    
    public static FileType GetFileType(ReadOnlySpan<byte> head, long length)
    {
        if (length <= 7 || head.Length < 7)
            return FileType.Unknown;

        if (head[..7].SequenceEqual("UnityFS"u8))
            return FileType.BundleFile;

        if (length < 20 || head.Length < 20)
            return FileType.Unknown;

        var fileSize = (ulong)BinaryPrimitives.ReadUInt32BigEndian(head[4..]);
        var version = (SerializedFileFormatVersion)BinaryPrimitives.ReadUInt32BigEndian(head[8..]);
        ulong dataOffset = BinaryPrimitives.ReadUInt32BigEndian(head[12..]);

        if (version >= SerializedFileFormatVersion.LargeFilesSupport)
        {
            if (length < 48 || head.Length < 48)
                return FileType.Unknown;

            fileSize = BinaryPrimitives.ReadUInt64BigEndian(head[24..]);
            dataOffset = BinaryPrimitives.ReadUInt64BigEndian(head[32..]);
        }

        return (long)fileSize == length && (long)dataOffset <= length
            ? FileType.SerializedFile
            : FileType.ResourceFile;
    }
}