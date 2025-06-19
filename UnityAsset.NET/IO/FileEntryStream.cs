using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO;

public class FileEntryStream : Stream
{
    private readonly BlockStream _blockStream;
    private readonly FileEntry _fileEntry;
    private long _positionInEntry;

    public FileEntryStream(BlockStream blockStream, FileEntry fileEntry)
    {
        _blockStream = blockStream;
        _fileEntry = fileEntry;
        _blockStream.Seek(fileEntry.Offset, SeekOrigin.Begin);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _fileEntry.Size;

    public override long Position
    {
        get => _positionInEntry;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _fileEntry.Size - _positionInEntry;
        if (remaining <= 0)
            return 0;
            
        count = (int)Math.Min(count, remaining);
        int bytesRead = _blockStream.Read(buffer, offset, count);
        _positionInEntry += bytesRead;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _positionInEntry + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };
        
        if (newPosition < 0 || newPosition > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        _positionInEntry = newPosition;
        _blockStream.Seek(_fileEntry.Offset + newPosition, SeekOrigin.Begin);
        return newPosition;
    }

    // Other required Stream methods
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}