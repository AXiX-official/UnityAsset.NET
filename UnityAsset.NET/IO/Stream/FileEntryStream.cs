using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO.Stream;

public class FileEntryStreamProvider : IStreamProvider
{
    private readonly IStreamProvider _streamProvider;
    private readonly FileEntry _fileEntry;

    public FileEntryStreamProvider(IStreamProvider streamProvider, FileEntry fileEntry)
    {
        _streamProvider = streamProvider;
        _fileEntry = fileEntry;
    }

    public System.IO.Stream OpenStream() => new FileEntryStream(_streamProvider, _fileEntry);
}

public class FileEntryStream : System.IO.Stream
{
    private readonly System.IO.Stream _blockStream;
    private readonly FileEntry _fileEntry;
    private long _position;

    public FileEntryStream(IStreamProvider streamProvider, FileEntry fileEntry)
    {
        _blockStream = streamProvider.OpenStream();
        _fileEntry = fileEntry;
        _blockStream.Seek(_fileEntry.Offset, SeekOrigin.Begin);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _fileEntry.Size;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int ReadByte()
    {
        _position++;
        if (_position > Length)
            throw new EndOfStreamException();
        return _blockStream.ReadByte();
    }

    public override int Read(Span<byte> buffer)
    {
        long remaining = _fileEntry.Size - _position;
        if (remaining <= 0)
            return 0;
            
        var count = (int)Math.Min(buffer.Length, remaining);
        int bytesRead = _blockStream.Read(buffer.Slice(0, count));
        _position += bytesRead;
        if (_position > Length)
            throw new EndOfStreamException();
        return bytesRead;
    }
    
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));


    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };
        
        if (newPosition < 0 || newPosition > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        
        _position = newPosition;
        _blockStream.Seek(_fileEntry.Offset + newPosition, SeekOrigin.Begin);
        return _position;
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}