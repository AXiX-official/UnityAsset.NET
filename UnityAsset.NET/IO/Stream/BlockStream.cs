using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.IO.Stream;

public class BlockStream : System.IO.Stream
{
    private readonly record struct BlockInfo(
        uint UncompressedSize,
        uint CompressedSize,
        long CompressedOffset,
        long UncompressedOffset,
        CompressionType CompressionType);

    private readonly List<BlockInfo> _blocks;
    private readonly CustomStreamReader _baseReader;
    private byte[]? _uncompressedBuffer;
    private MemoryStream? _currentBlockData;
    private int _currentBlockIndex = -1;
    private long _position;
    private readonly long _length;
    private readonly long _baseOffset;
    private readonly UnityCN? _unityCnInfo;
    private bool _disposed;

    public BlockStream(List<StorageBlockInfo> blocks, CustomStreamReader baseReader, UnityCN? unityCnInfo = null)
    {
        _blocks = new List<BlockInfo>(blocks.Count);
        long currentOffset = 0;
        long uncompressedOffset = 0;
        foreach (var block in blocks)
        {
            var compressionType = (CompressionType)(block.Flags & StorageBlockFlags.CompressionTypeMask);
            _blocks.Add(new BlockInfo(block.UncompressedSize, block.CompressedSize, currentOffset, uncompressedOffset, compressionType));
            currentOffset += block.CompressedSize;
            uncompressedOffset += block.UncompressedSize;
        }
        
        _baseReader = baseReader;
        _baseOffset = baseReader.Position;
        _length = uncompressedOffset;
        _unityCnInfo = unityCnInfo;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    private void EnsureBlockLoaded(long position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BlockStream));
        
        int low = 0;
        int high = _blocks.Count - 1;
        int blockIndex = -1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var block = _blocks[mid];
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

        if (blockIndex != _currentBlockIndex)
        {
            var block = _blocks[blockIndex];
            _baseReader.Seek(_baseOffset + block.CompressedOffset);
            var compressedData = _baseReader.ReadBytes((int)block.CompressedSize);
            
            if (_uncompressedBuffer == null || _uncompressedBuffer.Length < block.UncompressedSize)
            {
                _uncompressedBuffer = new byte[block.UncompressedSize];
            }

            if (_unityCnInfo != null && (block.CompressionType == CompressionType.Lz4 || block.CompressionType == CompressionType.Lz4HC))
            {
                _unityCnInfo.DecryptBlock(compressedData, compressedData.Length, blockIndex);
            }
            
            Compression.DecompressToBytes(compressedData, _uncompressedBuffer.AsSpan(0, (int)block.UncompressedSize), block.CompressionType);
            
            _currentBlockData?.Dispose();
            _currentBlockData = new MemoryStream(_uncompressedBuffer, 0, (int)block.UncompressedSize, false, true);
            _currentBlockIndex = blockIndex;
        }
        
        _currentBlockData!.Position = position - _blocks[blockIndex].UncompressedOffset;
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
            _position += bytesRead;
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
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };

        if (newPosition < 0 || newPosition >= _length)
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        _position = newPosition;
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