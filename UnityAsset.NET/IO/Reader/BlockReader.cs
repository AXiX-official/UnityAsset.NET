using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader;

public readonly record struct BlockInfo(
    uint UncompressedSize,
    uint CompressedSize,
    ulong CompressedOffset,
    ulong UncompressedOffset,
    CompressionType CompressionType);

public class BlockReaderProvider : IReaderProvider
{
    public readonly BlockInfo[] Blocks;
    public readonly IVirtualFile File;
    private readonly UnityCN? _unityCnInfo;
    
    public BlockReaderProvider(StorageBlockInfo[] blocks, IVirtualFile file,
        UnityCN? unityCnInfo = null)
    {
        Blocks = new BlockInfo[blocks.Length];
        ulong currentOffset = 0;
        ulong uncompressedOffset = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            var block = blocks[i];
            var compressionType = (CompressionType)(block.Flags & StorageBlockFlags.CompressionTypeMask);
            Blocks[i] = new BlockInfo(
                block.UncompressedSize, 
                block.CompressedSize, 
                currentOffset, 
                uncompressedOffset, 
                compressionType
            );
            currentOffset += block.CompressedSize;
            uncompressedOffset += block.UncompressedSize;
        }
        
        File = file;
        _unityCnInfo = unityCnInfo;
    }

    public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new BlockReader(Blocks, File.Clone(), endian, _unityCnInfo);
}

public class BlockReader : IReader
{
    public readonly BlockInfo[] Blocks;
    public readonly IVirtualFile File;
    private byte[] _buffer = [];
    private ulong _posOffset;
    private ulong _bufferSize;
    private int _currentBlockIndex = -1;
    private readonly UnityCN? _unityCnInfo;

    private ulong BufferPos => (ulong)Position - _posOffset;
    private ulong BufferRemaining => _bufferSize - BufferPos;

    #region Cache

    public static BlockCache Cache = new(maxSize: Setting.DefaultBlockCacheSize);

    public static ConcurrentDictionary<BlockCacheKey, (int parsed, int total, long size)> AssetToBlockCache = new();

    public static void RegisterAssetToBlockMap(SlicedReaderProvider srp, BlockReaderProvider brp, SerializedFile sf)
    {
        var offset = srp.Offset;
        var blocks = brp.Blocks;
        foreach (var info in sf.Metadata.AssetInfos)
        {
            var pos = offset + sf.Header.DataOffset + info.ByteOffset;
            var index = FindBlockIndex(blocks, pos);
            while (index < blocks.Length && pos + info.ByteSize > blocks[index].UncompressedOffset)
            {
                var block = blocks[index];
                index++;
                if (block.CompressionType == CompressionType.None)
                    continue;
                var key = new BlockCacheKey(brp.File, index);
                var size = block.UncompressedSize;
                AssetToBlockCache.AddOrUpdate(key,
                    addValue: (0, 1, size),
                    updateValueFactory: (k, existing) =>
                        (existing.parsed, existing.total + 1, size));
            }
        }
    }

    public static long TotalBlockSize => AssetToBlockCache.Values.Select(v => v.size).Sum();

    public static void RemoveSingleReferenceBlocks()
    {
        var keysToRemove = AssetToBlockCache
            .Where(kvp => kvp.Value.total <= 2)
            .Select(kvp => kvp.Key);
        foreach (var key in keysToRemove)
            AssetToBlockCache.TryRemove(key, out _);
    }

    public static void OnAssetParsed(Asset asset)
    {
        var sf = asset.SourceFile;
        if (sf.ReaderProvider is SlicedReaderProvider srp)
        {
            if (srp.BaseReaderProvider is BlockReaderProvider brp)
            {
                var offset = srp.Offset;
                var blocks = brp.Blocks;
                var pos = offset + sf.Header.DataOffset + asset.Info.ByteOffset;
                var index = FindBlockIndex(blocks, pos);
                while (index < blocks.Length && pos + asset.Info.ByteSize > blocks[index].UncompressedOffset)
                {
                    var key = new BlockCacheKey(brp.File, index);
                    var size = blocks[index].UncompressedSize;
                    var newStats = AssetToBlockCache.AddOrUpdate(key,
                        addValue: (-1, 1, 0),
                        updateValueFactory: (_, existing) =>
                            (existing.parsed + 1, existing.total, size));
                    // this over counted because currently asset uses WeakReference
                    // but it somehow makes it fast and low memory cost
                    // need more test
                    if (newStats.parsed == newStats.total)
                    {
                        Cache.Remove(key);
                    }

                    index++;
                }
            }
        }
    }

    #endregion
    
    

    public BlockReader(BlockInfo[] blocks, IVirtualFile file, Endianness endian = Endianness.BigEndian,
        UnityCN? unityCnInfo = null)
    {
        Blocks = blocks;
        File = file;
        Length = (long)(blocks[^1].UncompressedOffset + blocks[^1].UncompressedSize);
        _unityCnInfo = unityCnInfo;
        Endian = endian;
    }

    #region ISeek

    public long Position { get; set; }

    public long Length { get; }

    #endregion

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
        File.Position = (long)block.CompressedOffset;

        var compressedBuffer = ArrayPool<byte>.Shared.Rent((int)block.CompressedSize);
        var compressedSpan = compressedBuffer.AsSpan(0, (int)block.CompressedSize);
        try
        {
            File.ReadExactly(compressedSpan);
            var uncompressedBuffer = ArrayPool<byte>.Shared.Rent((int)block.UncompressedSize);
            var uncompressedSpan = uncompressedBuffer.AsSpan(0, (int)block.UncompressedSize);
            try
            {
                if (_unityCnInfo != null && (block.CompressionType == CompressionType.Lz4 ||
                                             block.CompressionType == CompressionType.Lz4HC))
                {
                    _unityCnInfo.DecryptAndDecompress(compressedSpan, uncompressedSpan, index);
                }
                else
                {
                    Compression.DecompressToBytes(compressedSpan, uncompressedSpan, block.CompressionType);
                }
                return uncompressedBuffer;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(uncompressedBuffer);
                throw;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressedBuffer);
        }
    }

    private void EnsureBlockLoaded(long position)
    {
        var blockIndex = _currentBlockIndex;
        if (_currentBlockIndex == -1 || (ulong)position < _posOffset || (ulong)position >= _posOffset + _bufferSize)
        {
            blockIndex = FindBlockIndex(Blocks, (ulong)position);
            if (blockIndex == -1)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
        }
        
        if (blockIndex != _currentBlockIndex)
        {
            var block = Blocks[blockIndex];
            var key = new BlockCacheKey(File, blockIndex);
            
            _buffer = AssetToBlockCache.ContainsKey(key)
                ? Cache.GetOrCreate(
                    key: new(File, blockIndex),
                    factory: () => DecompressBlock(blockIndex),
                    size: block.UncompressedSize
                )
                : DecompressBlock(blockIndex);
            _currentBlockIndex = blockIndex;
            _bufferSize = block.UncompressedSize;
            _posOffset = block.UncompressedOffset;
        }
    }

    # region IReader

    public Endianness Endian { get; set; }

    public int Read(Span<byte> buffer, int offset, int count)
    {
        int written = 0;
        while (written < buffer.Length - offset && ((IReader)this).Remaining > 0)
        {
            EnsureBlockLoaded(Position);

            var toCopy = Math.Min(count - written, (int)BufferRemaining);

            _buffer.AsSpan((int)BufferPos, toCopy)
                .CopyTo(buffer.Slice(offset + written, toCopy));

            Position += (uint)toCopy;
            written += toCopy;
        }

        return written;
    }
    public byte ReadByte()
    {
        EnsureBlockLoaded(Position);
        var ret = _buffer[BufferPos];
        Position++;
        return ret;
    }

    public byte[] ReadBytes(int count)
    {
        if ((uint)count > ((IReader)this).Remaining)
            throw new EndOfStreamException();
        byte[] bytes = new byte[count];
        ((IReader)this).ReadExactly(bytes);
        return bytes;
    }
    public short ReadInt16()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 2)
        {
            var span = _buffer.AsSpan((int)BufferPos, 2);
            Position += 2;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(span)
                : BinaryPrimitives.ReadInt16LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[2];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(tmp)
            : BinaryPrimitives.ReadInt16LittleEndian(tmp);
    }
    public ushort ReadUInt16()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 2)
        {
            var span = _buffer.AsSpan((int)BufferPos, 2);
            Position += 2;
            
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[2];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(tmp)
            : BinaryPrimitives.ReadUInt16LittleEndian(tmp);
    }
    public int ReadInt32()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 4)
        {
            var span = _buffer.AsSpan((int)BufferPos, 4);
            Position += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span)
                : BinaryPrimitives.ReadInt32LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(tmp)
            : BinaryPrimitives.ReadInt32LittleEndian(tmp);
    }
    public uint ReadUInt32()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 4)
        {
            var span = _buffer.AsSpan((int)BufferPos, 4);
            Position += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(span)
                : BinaryPrimitives.ReadUInt32LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(tmp)
            : BinaryPrimitives.ReadUInt32LittleEndian(tmp);
    }
    public long ReadInt64()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 8)
        {
            var span = _buffer.AsSpan((int)BufferPos, 8);
            Position += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(span)
                : BinaryPrimitives.ReadInt64LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(tmp)
            : BinaryPrimitives.ReadInt64LittleEndian(tmp);
    }
    public ulong ReadUInt64()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 8)
        {
            var span = _buffer.AsSpan((int)BufferPos, 8);
            Position += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(span)
                : BinaryPrimitives.ReadUInt64LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt64BigEndian(tmp)
            : BinaryPrimitives.ReadUInt64LittleEndian(tmp);
    }
    public float ReadSingle()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 4)
        {
            var span = _buffer.AsSpan((int)BufferPos, 4);
            Position += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(span)
                : BinaryPrimitives.ReadSingleLittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadSingleBigEndian(tmp)
            : BinaryPrimitives.ReadSingleLittleEndian(tmp);
    }
    public double ReadDouble()
    {
        EnsureBlockLoaded(Position);
        if (BufferRemaining >= 8)
        {
            var span = _buffer.AsSpan((int)BufferPos, 8);
            Position += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(span)
                : BinaryPrimitives.ReadDoubleLittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ((IReader)this).ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadDoubleBigEndian(tmp)
            : BinaryPrimitives.ReadDoubleLittleEndian(tmp);
    }
    public string ReadNullTerminatedString()
    {
        EnsureBlockLoaded(Position);
        
        var currentBlockRemaining = (int)BufferRemaining;
        var span = _buffer.AsSpan((int)BufferPos, currentBlockRemaining);
        
        int nullIndex = span.IndexOf((byte)0);
        
        if (nullIndex >= 0)
        {
            string result = Encoding.UTF8.GetString(span.Slice(0, nullIndex));
            var advance = nullIndex + 1;
            Position += advance;
            return result;
        }
        
        using var ms = new MemoryStream();
        while (true)
        {
            if (BufferPos >= _bufferSize)
            {
                EnsureBlockLoaded(Position);
            }
            byte b = _buffer[BufferPos];
            Position++;
            if (b == 0)
                break;
            ms.WriteByte(b);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    #endregion
}