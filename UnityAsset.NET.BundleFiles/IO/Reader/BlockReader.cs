using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader
{

    #if NETSTANDARD2_1
    public struct BlockInfo
    {
        public readonly uint UncompressedSize;
        public readonly uint CompressedSize;
        public readonly ulong CompressedOffset;
        public readonly ulong UncompressedOffset;
        public readonly CompressionType CompressionType;

        public BlockInfo(uint uncompressedSize, uint compressedSize, ulong compressedOffset, ulong uncompressedOffset, CompressionType compressionType)
        {
            UncompressedSize = uncompressedSize;
            CompressedSize = compressedSize;
            CompressedOffset = compressedOffset;
            UncompressedOffset = uncompressedOffset;
            CompressionType = compressionType;
        }
    }
    #else
    public readonly record struct BlockInfo(
        uint UncompressedSize,
        uint CompressedSize,
        ulong CompressedOffset,
        ulong UncompressedOffset,
        CompressionType CompressionType);
    #endif

    public class BlockReaderProvider : IReaderProvider
    {
        public readonly BlockInfo[] Blocks;
        public readonly IVirtualFile File;
        private readonly UnityCN? _unityCnInfo;
        private readonly BlockCacheContext _context;

        public BlockReaderProvider(StorageBlockInfo[] blocks, IVirtualFile file,
            UnityCN? unityCnInfo = null, BlockCacheContext? context = null)
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
            _context = context ?? BlockCacheContext.Shared;
        }

        public IReader CreateReader(Endianness endian = Endianness.BigEndian) =>
            new BlockReader(Blocks, File.Clone(), endian, _unityCnInfo, _context);
    }

    public class BlockReader : IReader
    {
        public readonly BlockInfo[] Blocks;
        public readonly IVirtualFile File;
        private byte[] _buffer = Array.Empty<byte>();
        private ulong _posOffset;
        private ulong _bufferSize;
        private int _currentBlockIndex = -1;
        private readonly UnityCN? _unityCnInfo;

        private ulong BufferPos => (ulong)Position - _posOffset;
        private ulong BufferRemaining => _bufferSize - BufferPos;

        private readonly BlockCacheContext _context;

        public BlockReader(BlockInfo[] blocks, IVirtualFile file, Endianness endian = Endianness.BigEndian,
            UnityCN? unityCnInfo = null, BlockCacheContext? context = null)
        {
            if (blocks.Length == 0)
                throw new ArgumentException("A block reader needs at least one block.", nameof(blocks));

            Blocks = blocks;
            File = file;
            Length = (long)(blocks[^1].UncompressedOffset + blocks[^1].UncompressedSize);
            _unityCnInfo = unityCnInfo;
            _context = context ?? BlockCacheContext.Shared;
            Endian = endian;
        }

        #region ISeek

        private long _position;

        /// <summary>
        /// The cursor in the decompressed stream, kept inside <c>[0, Length]</c>. Without the check a position past the
        /// last block reached <see cref="EnsureBlockLoaded"/> and came back as an <c>ArgumentOutOfRangeException</c>
        /// naming a parameter the caller never passed.
        /// </summary>
        public long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"Position {value} is outside a {Length}-byte block reader.");

                _position = value;
            }
        }

        public long Length { get; }

        #endregion

        public static int FindBlockIndex(BlockInfo[] blocks, ulong position)
        {
            int low = 0;
            int high = blocks.Length - 1;
            int blockIndex = -1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                var block = blocks[mid];
                if (position >= block.UncompressedOffset &&
                    position < block.UncompressedOffset + block.UncompressedSize)
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
                    // The position is inside the layout (the setter guarantees it) but the blocks do not cover it, which
                    // means the layout itself is short. Reported as "no data here" with the numbers, not as a bad
                    // argument: `position` is not a parameter the caller passed.
                    throw new EndOfStreamException(
                        $"Position {position} is not covered by the {Blocks.Length} blocks of this reader " +
                        $"(total uncompressed length {Length}).");
                }
            }

            if (blockIndex != _currentBlockIndex)
            {
                var block = Blocks[blockIndex];
                var key = new BlockCacheKey(File, blockIndex);

                _buffer = _context.AssetToBlockCache.ContainsKey(key)
                    ? _context.Cache.GetOrCreate(
                        key: new BlockCacheKey(File, blockIndex),
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
            IReader.ValidCount(buffer.Length, offset, count, nameof(offset), nameof(count));
            if (count == 0)
                return 0;

            var end = offset + count;
            var written = 0;
            while (offset + written < end)
            {
                if (Position < 0 || (ulong)Position >= (ulong)Length)
                    break;
                if (FindBlockIndex(Blocks, (ulong)Position) < 0)
                    break;

                EnsureBlockLoaded(Position);

                var toCopy = Math.Min(end - (offset + written), (int)BufferRemaining);
                if (toCopy <= 0)
                    break;

                _buffer.AsSpan((int)BufferPos, toCopy).CopyTo(buffer.Slice(offset + written, toCopy));
                Position += (uint)toCopy;
                written += toCopy;
            }

            return written;
        }

        public byte ReadByte()
        {
            // "No more data" is the truthful answer at the end of the last block, not a bad argument: the cursor is
            // always inside the block layout now, so reaching the end is the only way to get here. Without this guard the
            // lookup below fails and reports ArgumentOutOfRangeException("position"), a parameter the caller never passed.
            if (Position >= Length)
                throw new EndOfStreamException();

            EnsureBlockLoaded(Position);
            var ret = _buffer[BufferPos];
            Position++;
            return ret;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
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

                #if NETSTANDARD2_1
                var intBits = Endian == Endianness.BigEndian
                    ? BinaryPrimitives.ReadInt32BigEndian(span)
                    : BinaryPrimitives.ReadInt32LittleEndian(span);
                return BitConverter.Int32BitsToSingle(intBits);
                #else
                return Endian == Endianness.BigEndian
                    ? BinaryPrimitives.ReadSingleBigEndian(span)
                    : BinaryPrimitives.ReadSingleLittleEndian(span);
                #endif
            }

            Span<byte> tmp = stackalloc byte[4];
            ((IReader)this).ReadExactly(tmp);
            #if NETSTANDARD2_1
            var tmpBits = Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(tmp)
                : BinaryPrimitives.ReadInt32LittleEndian(tmp);
            return BitConverter.Int32BitsToSingle(tmpBits);
            #else
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(tmp)
                : BinaryPrimitives.ReadSingleLittleEndian(tmp);
            #endif
        }

        public double ReadDouble()
        {
            EnsureBlockLoaded(Position);
            if (BufferRemaining >= 8)
            {
                var span = _buffer.AsSpan((int)BufferPos, 8);
                Position += 8;

                #if NETSTANDARD2_1
                var intBits = Endian == Endianness.BigEndian
                    ? BinaryPrimitives.ReadInt64BigEndian(span)
                    : BinaryPrimitives.ReadInt64LittleEndian(span);
                return BitConverter.Int64BitsToDouble(intBits);
                #else
                return Endian == Endianness.BigEndian
                    ? BinaryPrimitives.ReadDoubleBigEndian(span)
                    : BinaryPrimitives.ReadDoubleLittleEndian(span);
                #endif
            }

            Span<byte> tmp = stackalloc byte[8];
            ((IReader)this).ReadExactly(tmp);
            #if NETSTANDARD2_1
            var tmpBits = Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(tmp)
                : BinaryPrimitives.ReadInt64LittleEndian(tmp);
            return BitConverter.Int64BitsToDouble(tmpBits);
            #else
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(tmp)
                : BinaryPrimitives.ReadDoubleLittleEndian(tmp);
            #endif
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
}