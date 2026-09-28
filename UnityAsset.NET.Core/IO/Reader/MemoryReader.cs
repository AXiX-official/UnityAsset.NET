using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO.Reader
{
    public class MemoryReader : IReader
    {
        private readonly Memory<byte> _data;
        private int _position;

        public MemoryReader(byte[] data, int position = 0, Endianness endian = Endianness.BigEndian)
        {
            _data = data;
            _position = position;
            Endian = endian;
        }

        public MemoryReader(Memory<byte> data, int position = 0, Endianness endian = Endianness.BigEndian)
        {
            _data = data;
            _position = position;
            Endian = endian;
        }

        // TEMPORARY: make it writable
        public MemoryReader(int length = 0, Endianness endian = Endianness.BigEndian)
        {
            _data = new byte[length];
            _position = 0;
            Endian = endian;
        }

        public ReadOnlySpan<byte> AsReadOnlySpan => _data.Span;

        # region ISeek

        /// <summary>
        /// The cursor, which is always inside <c>[0, Length]</c>. <c>Length</c> itself is the only position a completed
        /// read leaves behind, so it is accepted; anything past it is refused here rather than turning into a negative
        /// <see cref="IReader.Remaining"/> that every loop on it would then read differently.
        /// </summary>
        public long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _data.Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"Position {value} is outside a {_data.Length}-byte reader.");

                _position = (int)value;
            }
        }

        public long Length => _data.Length;

        # endregion

        # region IReader

        public Endianness Endian { get; set; }

        public int Read(Span<byte> buffer, int offset, int count)
        {
            IReader.ValidCount(buffer.Length, offset, count, nameof(offset), nameof(count));

            var bytesToRead = Math.Min(count, Math.Max(0, _data.Length - _position));
            if (bytesToRead == 0)
                return 0;

            ReadOnlySpanBytes(bytesToRead).CopyTo(buffer.Slice(offset, bytesToRead));
            return bytesToRead;
        }

        public byte ReadByte()
        {
            if (_position >= _data.Length)
                throw new EndOfStreamException();
            return _data.Span[_position++];
        }

        private ReadOnlySpan<byte> ReadOnlySpanBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (count == 0)
                return ReadOnlySpan<byte>.Empty;
            var span = _data.Span.Slice(_position, count);
            _position += count;
            return span;
        }

        public byte[] ReadBytes(int count) => ReadOnlySpanBytes(count).ToArray();

        public Int16 ReadInt16()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(ReadOnlySpanBytes(2))
                : BinaryPrimitives.ReadInt16LittleEndian(ReadOnlySpanBytes(2));
        }

        public UInt16 ReadUInt16()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(ReadOnlySpanBytes(2))
                : BinaryPrimitives.ReadUInt16LittleEndian(ReadOnlySpanBytes(2));
        }

        public Int32 ReadInt32()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpanBytes(4))
                : BinaryPrimitives.ReadInt32LittleEndian(ReadOnlySpanBytes(4));
        }

        public UInt32 ReadUInt32()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(ReadOnlySpanBytes(4))
                : BinaryPrimitives.ReadUInt32LittleEndian(ReadOnlySpanBytes(4));
        }

        public Int64 ReadInt64()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(ReadOnlySpanBytes(8))
                : BinaryPrimitives.ReadInt64LittleEndian(ReadOnlySpanBytes(8));
        }

        public UInt64 ReadUInt64()
        {
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(ReadOnlySpanBytes(8))
                : BinaryPrimitives.ReadUInt64LittleEndian(ReadOnlySpanBytes(8));
        }

        public float ReadSingle()
        {
            #if NETSTANDARD2_1
            var intBits = Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpanBytes(4))
                : BinaryPrimitives.ReadInt32LittleEndian(ReadOnlySpanBytes(4));
            return BitConverter.Int32BitsToSingle(intBits);
            #else
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(ReadOnlySpanBytes(4))
                : BinaryPrimitives.ReadSingleLittleEndian(ReadOnlySpanBytes(4));
            #endif
        }

        public double ReadDouble()
        {
            #if NETSTANDARD2_1
            var intBits = Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(ReadOnlySpanBytes(8))
                : BinaryPrimitives.ReadInt64LittleEndian(ReadOnlySpanBytes(8));
            return BitConverter.Int64BitsToDouble(intBits);
            #else
            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(ReadOnlySpanBytes(8))
                : BinaryPrimitives.ReadDoubleLittleEndian(ReadOnlySpanBytes(8));
            #endif
        }

        public string ReadNullTerminatedString()
        {
            var span = _data.Span.Slice(_position, _data.Length - _position);
            int nullTerminator = span.IndexOf((byte)0);
            if (nullTerminator < 0)
                throw new IndexOutOfRangeException("Null terminator not found.");
            var strBytes = span.Slice(0, nullTerminator);
            _position += nullTerminator + 1;
            return Encoding.UTF8.GetString(strBytes);
        }

        # endregion

        // TEMPORARY: make it writable
        public Span<byte> AsWritableSpan => _data.Span;
    }

    public class MemoryReaderProvider : IReaderProvider
    {
        private readonly Memory<byte> _data;

        public MemoryReaderProvider(byte[] data)
        {
            _data = new Memory<byte>(data);
        }

        public MemoryReaderProvider(Memory<byte> data)
        {
            _data = data;
        }

        public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new MemoryReader(_data, 0, endian);
    }
}