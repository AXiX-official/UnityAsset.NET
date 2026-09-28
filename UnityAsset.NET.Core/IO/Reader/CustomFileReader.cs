using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader
{
    public class CustomFileReader : IReader
    {
        private readonly IVirtualFile _data;

        private readonly byte[] _buffer;
        private uint _bufferPos;
        private uint _bufferSize;
        private ulong BufferRemaining => _bufferSize - _bufferPos;

        public CustomFileReader(IVirtualFileInfo fileInfo, Endianness endian = Endianness.BigEndian,
            int bufferSize = 8192)
        {
            _data = fileInfo.GetFile();
            Endian = endian;

            _buffer = new byte[bufferSize];
        }

        private bool TryFillBuffer()
        {
            _data.Position = Position;

            _bufferPos = 0;
            _bufferSize = _data.Read(_buffer, 0, (uint)_buffer.Length);
            if (_bufferSize == 0)
            {
                _data.Position = Length;
                return false;
            }

            return true;
        }

        private void EnsureFilled()
        {
            if (_bufferPos >= _bufferSize && !TryFillBuffer())
                throw new EndOfStreamException();
        }

        private void FillBuffer() => EnsureFilled();

        # region ISeek

        public long Position
        {
            get => _data.Position - _bufferSize + _bufferPos;
            set
            {
                // Validated before the window is touched, so a refused position leaves the reader exactly as it was.
                // `Length` is accepted: it is where a completed read leaves the cursor.
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"Position {value} is outside a {Length}-byte reader.");

                var currentPos = Position;
                if (currentPos == value)
                {
                    return;
                }

                // Moving behind the cursor has to go through the stream: the cursor is the logical end of the data, so
                // there is nothing buffered there to walk back into. Rewinding `_bufferPos` instead would leave the track
                // between the new position and the cursor with no data, and Finish writes only up to `_bufferPos`, so the
                // bytes that were already written would be dropped.
                if (value < currentPos)
                {
                    _bufferPos = 0;
                    _bufferSize = 0;
                    _data.Position = value;
                    return;
                }

                if (value < currentPos + (long)BufferRemaining)
                {
                    _bufferPos += (uint)(value - currentPos);
                    return;
                }

                _bufferPos = 0;
                _bufferSize = 0;
                _data.Position = value;
            }
        }

        public long Length => _data.Length;

        # endregion

        # region IReader

        public Endianness Endian { get; set; }

        public int Read(Span<byte> buffer, int offset, int count)
        {
            IReader.ValidCount(buffer.Length, offset, count, nameof(offset), nameof(count));
            if (count == 0)
                return 0;

            var end = offset + count;
            var written = 0;
            while (offset + written < end && EnsureWindow())
            {
                var toCopy = Math.Min(end - (offset + written), (int)BufferRemaining);
                _buffer.AsSpan((int)_bufferPos, toCopy).CopyTo(buffer.Slice(offset + written, toCopy));
                Position += (uint)toCopy;
                written += toCopy;
            }

            return written;
        }

        /// <summary>
        /// True while the window holds an unread byte; refills when it is empty, and gives up at end of file.
        /// <para>
        /// The loop in <see cref="Read"/> relies on the returned window being non-empty, which is what keeps
        /// <c>toCopy</c> at one byte or more and therefore keeps the loop making progress. A "true but empty" answer
        /// here would spin forever.
        /// </para>
        /// </summary>
        private bool EnsureWindow() => _bufferPos < _bufferSize || TryFillBuffer();

        public byte ReadByte()
        {
            if (_bufferPos >= _bufferSize)
            {
                FillBuffer();
            }

            return _buffer[_bufferPos++];
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
            if (BufferRemaining >= 2)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 2);
                _bufferPos += 2;

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
            if (BufferRemaining >= 2)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 2);
                _bufferPos += 2;

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
            if (BufferRemaining >= 4)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 4);
                _bufferPos += 4;

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
            if (BufferRemaining >= 4)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 4);
                _bufferPos += 4;

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
            if (BufferRemaining >= 8)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 8);
                _bufferPos += 8;

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
            if (BufferRemaining >= 8)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 8);
                _bufferPos += 8;

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
            if (BufferRemaining >= 4)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 4);
                _bufferPos += 4;

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
            if (BufferRemaining >= 8)
            {
                var span = _buffer.AsSpan((int)_bufferPos, 8);
                _bufferPos += 8;

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
            using var ms = new MemoryStream();
            while (true)
            {
                if (_bufferPos >= _bufferSize)
                {
                    FillBuffer();
                }

                byte b = _buffer[_bufferPos++];
                if (b == 0)
                    break;
                ms.WriteByte(b);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        # endregion
    }

    public class CustomFileReaderProvider : IReaderProvider
    {
        public readonly IVirtualFileInfo FileInfo;

        public CustomFileReaderProvider(IVirtualFileInfo fileInfo)
        {
            FileInfo = fileInfo;
        }

        public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new CustomFileReader(FileInfo, endian);
    }
}