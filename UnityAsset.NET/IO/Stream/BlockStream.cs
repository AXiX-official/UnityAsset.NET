using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;

namespace UnityAsset.NET.IO.Stream;

public class BlockStreamProvider : IStreamProvider
{
    private readonly BlockStream.BlockInfo[] _blocks;
    private readonly IReaderProvider _baseReaderProvider;
    private readonly ulong _length;
    private readonly UnityCN? _unityCnInfo;
    
    public BlockStreamProvider(List<StorageBlockInfo> blocks, IReaderProvider readerProvider,
        UnityCN? unityCnInfo = null)
    {
        _blocks = new BlockStream.BlockInfo[blocks.Count];
        ulong currentOffset = 0;
        ulong uncompressedOffset = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var compressionType = (CompressionType)(block.Flags & StorageBlockFlags.CompressionTypeMask);
            _blocks[i] = new BlockStream.BlockInfo(
                block.UncompressedSize, 
                block.CompressedSize, 
                currentOffset, 
                uncompressedOffset, 
                compressionType
            );
            currentOffset += block.CompressedSize;
            uncompressedOffset += block.UncompressedSize;
        }
        
        _baseReaderProvider = readerProvider;
        _length = uncompressedOffset;
        _unityCnInfo = unityCnInfo;
    }

    public System.IO.Stream OpenStream() => new BlockStream(_blocks, _baseReaderProvider, _length, _unityCnInfo);
}

public class BlockStream : System.IO.Stream
{
    public readonly record struct BlockInfo(
        uint UncompressedSize,
        uint CompressedSize,
        ulong CompressedOffset,
        ulong UncompressedOffset,
        CompressionType CompressionType);

    public readonly BlockInfo[] Blocks;
    private readonly IReaderProvider _baseReaderProvider;
    private MemoryStream? _currentBlockData;
    public int CurrentBlockIndex = -1;
    private ulong _position;
    private readonly ulong _length;
    private readonly UnityCN? _unityCnInfo;
    private bool _disposed;

    public readonly record struct CacheKey(IReaderProvider Provider, int BlockIndex);
    
    public static CustomMemoryCache<CacheKey, byte[]> Cache = new (maxSize: Setting.DefaultBlockCacheSize);

    public BlockStream(BlockInfo[] blocks, IReaderProvider baseReaderProvider, ulong length, UnityCN? unityCnInfo = null)
    {
        Blocks = blocks;
        _baseReaderProvider = baseReaderProvider;
        _length = length;
        _unityCnInfo = unityCnInfo;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => (long)_length;

    public override long Position
    {
        get => (long)_position;
        set => Seek(value, SeekOrigin.Begin);
    }

    private void EnsureBlockLoaded(ulong position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlockStream));
        
        int low = 0;
        int high = Blocks.Length - 1;
        int blockIndex = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var block = Blocks[mid];
            if (position >= block.UncompressedOffset && position < block.UncompressedOffset + block.UncompressedSize)
            {
                blockIndex = mid;
                break;
            }
            if (position < block.UncompressedOffset)
            {
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        if (blockIndex == -1)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is beyond the end of the data");
        }

        if (blockIndex != CurrentBlockIndex)
        {
            var block = Blocks[blockIndex];

            var blockBuffer = Cache.GetOrCreate(
                key: new(_baseReaderProvider, blockIndex),
                factory: () =>
                {
                    using var reader = _baseReaderProvider.CreateReader();
                    reader.Position = (long)block.CompressedOffset;
                    var compressedData = reader.ReadBytes((int)block.CompressedSize);
                    if (_unityCnInfo != null && (block.CompressionType == CompressionType.Lz4 || block.CompressionType == CompressionType.Lz4HC))
                    {
                        _unityCnInfo.DecryptBlock(compressedData, compressedData.Length, blockIndex);
                    }
                    
                    var uncompressedBuffer = new byte[block.UncompressedSize];
                    Compression.DecompressToBytes(compressedData, uncompressedBuffer.AsSpan(0, (int)block.UncompressedSize), block.CompressionType);
                    
                    return uncompressedBuffer;
                },
                size: block.UncompressedSize
            );
            
            _currentBlockData = new MemoryStream(blockBuffer);
            
            CurrentBlockIndex = blockIndex;
        }
        
        _currentBlockData!.Position = (long)(position - Blocks[blockIndex].UncompressedOffset);
    }

    public override int ReadByte()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlockStream));
        if (_position >= _length)  throw new EndOfStreamException();
        
        EnsureBlockLoaded(_position);
        _position++;
        return _currentBlockData!.ReadByte();
    }

    public override int Read(Span<byte> buffer)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlockStream));
        
        var count = buffer.Length;
        var offset = 0;

        int totalRead = 0;
        while (count > 0)
        {
            if (_position >= _length) break;

            EnsureBlockLoaded(_position);

            long remainingInBlock = _currentBlockData!.Length - _currentBlockData.Position;
            int bytesToRead = (int)Math.Min(remainingInBlock, count);

            int bytesRead = _currentBlockData.Read(buffer.Slice(offset, bytesToRead));
            if (bytesRead <= 0) break;

            totalRead += bytesRead;
            offset += bytesRead;
            count -= bytesRead;
            _position += (uint)bytesRead;
        }

        return totalRead;
    }
    
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlockStream));

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => (long)(offset > 0 ? _position + (ulong)offset : _position - ((ulong)-offset)),
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };

        if (newPosition < 0 || newPosition > (long)_length)
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        _position = (ulong)newPosition;
        return newPosition;
    }
    
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentBlockData?.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}