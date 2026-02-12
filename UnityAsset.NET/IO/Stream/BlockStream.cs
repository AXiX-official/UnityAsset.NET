using System.Collections.Concurrent;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Reader;

namespace UnityAsset.NET.IO.Stream;

public class BlockStreamProvider : IStreamProvider
{
    public readonly BlockStream.BlockInfo[] Blocks;
    public readonly IReaderProvider BaseReaderProvider;
    private readonly ulong _length;
    private readonly UnityCN? _unityCnInfo;
    
    public BlockStreamProvider(List<StorageBlockInfo> blocks, IReaderProvider readerProvider,
        UnityCN? unityCnInfo = null)
    {
        Blocks = new BlockStream.BlockInfo[blocks.Count];
        ulong currentOffset = 0;
        ulong uncompressedOffset = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var compressionType = (CompressionType)(block.Flags & StorageBlockFlags.CompressionTypeMask);
            Blocks[i] = new BlockStream.BlockInfo(
                block.UncompressedSize, 
                block.CompressedSize, 
                currentOffset, 
                uncompressedOffset, 
                compressionType
            );
            currentOffset += block.CompressedSize;
            uncompressedOffset += block.UncompressedSize;
        }
        
        BaseReaderProvider = readerProvider;
        _length = uncompressedOffset;
        _unityCnInfo = unityCnInfo;
    }

    public System.IO.Stream OpenStream() => new BlockStream(Blocks, BaseReaderProvider, _length, _unityCnInfo);
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
    
    public static ConcurrentDictionary<CacheKey, (int parsed, int total)> AssetToBlockCache = new();

    public static void RegisterAssetToBlockMap(FileEntryStreamProvider fesp, BlockStreamProvider bsp, SerializedFile sf)
    {
        var offset = fesp.FileEntry.Offset;
        var blocks = bsp.Blocks;
        foreach (var info in sf.Metadata.AssetInfos)
        {
            var pos = offset + sf.Header.DataOffset + info.ByteOffset;
            var index = FindBlockIndex(blocks, pos);
            while (index < blocks.Length && pos + info.ByteSize > blocks[index].UncompressedOffset)
            {
                var key = new CacheKey(bsp.BaseReaderProvider, index);
                AssetToBlockCache.AddOrUpdate(key,
                    addValue: (0, 1),
                    updateValueFactory: (k, existing) => 
                        (existing.parsed, existing.total + 1));
                index++;
            }
        }
    }

    public static void OnAssetParsed(Asset asset)
    {
        var sf = asset.SourceFile;
        if (sf.ReaderProvider is CustomStreamReaderProvider csrp)
        {
            if (csrp.StreamProvider is FileEntryStreamProvider fesp)
            {
                if (fesp.StreamProvider is BlockStreamProvider bsp)
                {
                    var offset = fesp.FileEntry.Offset;
                    var blocks = bsp.Blocks;
                    var pos = offset + sf.Header.DataOffset + asset.Info.ByteOffset;
                    var index = FindBlockIndex(blocks, pos);
                    while (index < blocks.Length && pos + asset.Info.ByteSize > blocks[index].UncompressedOffset)
                    {
                        var key = new CacheKey(bsp.BaseReaderProvider, index);
                        var newStats = AssetToBlockCache.AddOrUpdate(key,
                            addValue: (0, 1),
                            updateValueFactory: (_, existing) => 
                                (existing.parsed + 1, existing.total));
                        if (newStats.parsed == newStats.total)
                        {
                            Cache.Remove(key);
                        }
                        index++;
                    }
                }
            }
        }
    }

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

    private static int FindBlockIndex(BlockInfo[] blocks, ulong position)
    {
        int low = 0;
        int high = blocks.Length - 1;
        int blockIndex = -1;
        
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            var block = blocks[mid];
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

        return blockIndex;
    }

    private byte[] DecompressBlock(int index)
    {
        var block = Blocks[index];
        using var reader = _baseReaderProvider.CreateReader();
        reader.Position = (long)block.CompressedOffset;
        var compressedData = reader.ReadBytes((int)block.CompressedSize);
        if (_unityCnInfo != null && (block.CompressionType == CompressionType.Lz4 || block.CompressionType == CompressionType.Lz4HC))
        {
            _unityCnInfo.DecryptBlock(compressedData, compressedData.Length, index);
        }
                    
        var uncompressedBuffer = new byte[block.UncompressedSize];
        Compression.DecompressToBytes(compressedData, uncompressedBuffer.AsSpan(0, (int)block.UncompressedSize), block.CompressionType);
                    
        return uncompressedBuffer;
    }

    private void EnsureBlockLoaded(ulong position)
    {
        var blockIndex = FindBlockIndex(Blocks, position);

        if (blockIndex == -1)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is beyond the end of the data");
        }

        if (blockIndex != CurrentBlockIndex)
        {
            var block = Blocks[blockIndex];
            var key = new CacheKey(_baseReaderProvider, blockIndex);

            var blockBuffer = AssetToBlockCache.ContainsKey(key)
                ? Cache.GetOrCreate(
                    key: new(_baseReaderProvider, blockIndex),
                    factory: () => DecompressBlock(blockIndex),
                    size: block.UncompressedSize
                )
                : DecompressBlock(blockIndex);
            
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