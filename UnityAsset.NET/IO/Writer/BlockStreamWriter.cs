using SevenZip.Compression.LZMA;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO.Writer;

// Only used for Bundle block Serializing
public abstract class BlockStreamWriter : IWriter, IDisposable
{
    public static long InMemorySizeLimit = 64 * 1024 * 1024; //64MB
    protected bool IsMemoryStream = true;
    protected readonly CompressionType CompressionType;
    protected readonly int ChunkSize;
    protected Stream Stream;
    private bool _finished;
    public List<StorageBlockInfo> BlockInfos = new();
    
    protected BlockStreamWriter(CompressionType compressionType, int chunkSize)
    {
        CompressionType = compressionType;
        ChunkSize = chunkSize;
        Stream = new MemoryStream();
    }

    public static BlockStreamWriter GetBlockWriter(CompressionType compressionType)
    {
        var defaultChunkSize = compressionType == CompressionType.Lzma ? Int32.MaxValue : Setting.DefaultChunkSize;
        return compressionType switch
        {
            CompressionType.None => new NoneCompressionBlockStreamWriter(compressionType, defaultChunkSize),
            CompressionType.Lz4 => new Lz4CompressionBlockStreamWriter(compressionType, defaultChunkSize),
            CompressionType.Lz4HC => new Lz4CompressionBlockStreamWriter(compressionType, defaultChunkSize),
            CompressionType.Lzma => new LzmaCompressionBlockStreamWriter(compressionType, defaultChunkSize),
            _ => throw new ArgumentOutOfRangeException(nameof(compressionType), compressionType, null)
        };
    }
    
    # region ISeek
    public long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
    public long Length => throw new NotImplementedException();
    # endregion
    
    # region IWriter
    
    public Endianness Endian { get; set; }
    public abstract void WriteByte(byte value);
    public abstract void WriteBytes(ReadOnlySpan<byte> bytes);

    public abstract ulong WriteBytes(IReader reader);
    # endregion

    public virtual void Finish()
    {
        _finished = true;
    }

    protected abstract void CheckMemoryLimit();

    public virtual void Dispose()
    {
        Stream.Dispose();
    }
    
    public Stream GetDataStream()
    {
        if (!_finished)
            throw new InvalidOperationException("Call Finish() first");

        Stream.Position = 0;
        return Stream;
    }
}

public class NoneCompressionBlockStreamWriter : BlockStreamWriter
{
    public NoneCompressionBlockStreamWriter(CompressionType compressionType, int chunkSize)
        : base(compressionType, chunkSize)
    {
        
    }
    
    public override void WriteByte(byte value)
    {
        Stream.WriteByte(value);
        CheckMemoryLimit();
    }

    public override void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        Stream.Write(bytes);
        CheckMemoryLimit();
    }

    public override ulong WriteBytes(IReader reader)
    {
        var pos = Stream.Position;
        var tempBuffer = new byte[8196];
        while (reader.Remaining > 0)
        {
            var bytesRead = reader.Read(tempBuffer, 0, 8196);
            Stream.Write(tempBuffer, 0, bytesRead);
            CheckMemoryLimit();
        }

        return (ulong)(Stream.Position - pos);
    }

    public override void Finish()
    {
        var totalSize = Stream.Length;
        while (totalSize > 0)
        {
            var chunkSize = Math.Min(totalSize, ChunkSize);
            BlockInfos.Add(new ((uint)chunkSize, (uint)chunkSize, (StorageBlockFlags)CompressionType.None));
            totalSize -= chunkSize;
        }
        base.Finish();
    }

    protected override void CheckMemoryLimit()
    {
        if (IsMemoryStream && Stream.Length >= InMemorySizeLimit)
        {
            var path = Path.GetTempFileName();
            var newStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            Stream.Position = 0;
            Stream.CopyTo(newStream);
            Stream.Dispose();
            Stream = newStream;
            IsMemoryStream = false;
        }
    }
}

public class Lz4CompressionBlockStreamWriter : BlockStreamWriter
{
    private readonly byte[] _buffer;
    private int _bufferPos;
    private int BuffRemaining => ChunkSize - _bufferPos;
    public Lz4CompressionBlockStreamWriter(CompressionType compressionType, int chunkSize)
        : base(compressionType, chunkSize)
    {
        _buffer = new byte[ChunkSize];
    }
    
    public override void WriteByte(byte value)
    {
        if (BuffRemaining == 0)
            FlushBlock();
        _buffer[_bufferPos++] = value;
    }

    public override void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        var remaining = bytes.Length;
        while (remaining > 0)
        {
            int toCopy = Math.Min(remaining, BuffRemaining);
            bytes.Slice(offset, toCopy)
                .CopyTo(_buffer.AsSpan(_bufferPos, toCopy));
            
            offset += toCopy;
            remaining -= toCopy;
            _bufferPos += toCopy;

            if (BuffRemaining == 0)
            {
                FlushBlock();
            }
        }
    }

    public override ulong WriteBytes(IReader reader)
    {
        ulong totalBytesRead = 0;
        while (reader.Remaining > 0)
        {
            var bytesRead = reader.Read(_buffer, _bufferPos, BuffRemaining);
            _bufferPos += bytesRead;
            totalBytesRead += (uint)bytesRead;
            
            if (BuffRemaining == 0)
            {
                FlushBlock();
            }
        }

        return totalBytesRead;
    }

    private void FlushBlock()
    {
        var compressed = Compression.CompressToStream(_buffer.AsSpan(0, _bufferPos), CompressionType);

        if (compressed.Length < ChunkSize)
        {
            compressed.CopyTo(Stream);
            BlockInfos.Add(new ((uint)_bufferPos, (uint)compressed.Length, (StorageBlockFlags)CompressionType));
        }
        else
        {
            Stream.Write(_buffer, 0, ChunkSize);
            BlockInfos.Add(new ((uint)ChunkSize, (uint)ChunkSize, (StorageBlockFlags)CompressionType.None));
        }
        CheckMemoryLimit();
        
        _bufferPos = 0;
    }
    
    public override void Finish()
    {
        if (_bufferPos > 0)
            FlushBlock();
        base.Finish();
    }
    
    protected override void CheckMemoryLimit()
    {
        if (IsMemoryStream && Stream.Length >= InMemorySizeLimit)
        {
            var path = Path.GetTempFileName();
            var newStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            Stream.Position = 0;
            Stream.CopyTo(newStream);
            Stream.Dispose();
            Stream = newStream;
            IsMemoryStream = false;
        }
    }
}

public class LzmaCompressionBlockStreamWriter : BlockStreamWriter
{
    private Stream _bufferStream;
    private long BuffRemaining => ChunkSize - _bufferStream.Position;
    private bool _isBufferInMemory = true;
    public LzmaCompressionBlockStreamWriter(CompressionType compressionType, int chunkSize)
        : base(compressionType, chunkSize)
    {
        _bufferStream = new MemoryStream();
    }
    
    public override void WriteByte(byte value)
    {
        if (BuffRemaining == 0)
            FlushBlock();
        _bufferStream.WriteByte(value);
        CheckMemoryLimit();
    }

    public override void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        var remaining = bytes.Length;
        while (remaining > 0)
        {
            int toCopy = (int)Math.Min(remaining, BuffRemaining);
            _bufferStream.Write(bytes.Slice(offset, toCopy));
            CheckMemoryLimit();
            
            offset += toCopy;
            remaining -= toCopy;

            if (BuffRemaining == 0)
            {
                FlushBlock();
            }
        }
    }

    public override ulong WriteBytes(IReader reader)
    {
        var tempBuffer = new byte[8196];
        var totalBytesRead = 0;
        while (reader.Remaining > 0)
        {
            var bytesRead = reader.Read(tempBuffer, 0, 8196);
            WriteBytes(tempBuffer.AsSpan(0, bytesRead));
            CheckMemoryLimit();
            totalBytesRead += bytesRead;
        }

        return (ulong)totalBytesRead;
    }

    private void FlushBlock()
    {
        var encoder = new Encoder();
        using var compressedStream = new MemoryStream();
        encoder.WriteCoderProperties(compressedStream);
        _bufferStream.Position = 0;
        encoder.Code(_bufferStream, compressedStream, -1, -1, null);
        
        BlockInfos.Add(new ((uint)_bufferStream.Length, (uint)compressedStream.Length, (StorageBlockFlags)CompressionType.Lzma));

        compressedStream.Position = 0;
        compressedStream.CopyTo(Stream);

        _bufferStream = new MemoryStream();
        _isBufferInMemory = true;
    }
    
    public override void Finish()
    {
        if (_bufferStream.Length > 0)
            FlushBlock();
        base.Finish();
    }
    
    protected override void CheckMemoryLimit()
    {
        if (_isBufferInMemory && _bufferStream.Length >= InMemorySizeLimit)
        {
            var path = Path.GetTempFileName();
            var newStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            _bufferStream.Position = 0;
            _bufferStream.CopyTo(newStream);
            _bufferStream.Dispose();
            _bufferStream = newStream;
            _isBufferInMemory = false;
        }
        
        if (IsMemoryStream && !_isBufferInMemory)
        {
            var path = Path.GetTempFileName();
            var newStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            Stream.Position = 0;
            Stream.CopyTo(newStream);
            Stream.Dispose();
            Stream = newStream;
            IsMemoryStream = false;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _bufferStream.Dispose();
    }
}