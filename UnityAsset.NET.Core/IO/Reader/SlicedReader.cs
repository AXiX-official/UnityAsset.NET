namespace UnityAsset.NET.IO.Reader
{
    public class SlicedReader : IReader
    {
        protected readonly IReader BaseReader;
        private readonly ulong _offset;

        public static Exception BeyondSlice(string what, long position, long length) =>
            new Exception($"Trying to read beyond slice range: {what} at offset {position} of a {length}-byte slice.");

        # region ISeek

        /// <summary>
        /// The cursor relative to the start of the slice, kept inside <c>[0, Length]</c>. The slice's own <c>Length</c> is
        /// the boundary rather than the base reader's: a position past the slice has no bytes to read even though the
        /// file below continues.
        /// </summary>
        public long Position
        {
            get => BaseReader.Position - (long)_offset;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        $"Position {value} is outside a {Length}-byte slice.");

                BaseReader.Position = (long)_offset + value;
            }
        }

        public long Length { get; }

        public Endianness Endian
        {
            get => BaseReader.Endian;
            set => BaseReader.Endian = value;
        }

        # endregion

        public int Read(Span<byte> buffer, int offset, int count)
        {
            IReader.ValidCount(buffer.Length, offset, count, nameof(offset), nameof(count));

            var bytesToRead = Math.Min(count, (int)Math.Max(0, Length - Position));
            if (bytesToRead == 0)
                return 0;

            return BaseReader.Read(buffer, offset, bytesToRead);
        }

        public byte ReadByte()
        {
            if (Position + 1 > Length)
                throw BeyondSlice("a byte", Position, Length);
            return BaseReader.ReadByte();
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (Position + count > Length)
                throw BeyondSlice($"{count} bytes", Position, Length);
            return BaseReader.ReadBytes(count);
        }

        public short ReadInt16()
        {
            if (Position + 2 > Length)
                throw BeyondSlice("2 bytes", Position, Length);
            return BaseReader.ReadInt16();
        }

        public ushort ReadUInt16()
        {
            if (Position + 2 > Length)
                throw BeyondSlice("2 bytes", Position, Length);
            return BaseReader.ReadUInt16();
        }

        public int ReadInt32()
        {
            if (Position + 4 > Length)
                throw BeyondSlice("4 bytes", Position, Length);
            return BaseReader.ReadInt32();
        }

        public uint ReadUInt32()
        {
            if (Position + 4 > Length)
                throw BeyondSlice("4 bytes", Position, Length);
            return BaseReader.ReadUInt32();
        }

        public long ReadInt64()
        {
            if (Position + 8 > Length)
                throw BeyondSlice("8 bytes", Position, Length);
            return BaseReader.ReadInt64();
        }

        public ulong ReadUInt64()
        {
            if (Position + 8 > Length)
                throw BeyondSlice("8 bytes", Position, Length);
            return BaseReader.ReadUInt64();
        }

        public float ReadSingle()
        {
            if (Position + 4 > Length)
                throw BeyondSlice("4 bytes", Position, Length);
            return BaseReader.ReadSingle();
        }

        public double ReadDouble()
        {
            if (Position + 8 > Length)
                throw BeyondSlice("8 bytes", Position, Length);
            return BaseReader.ReadDouble();
        }

        public string ReadNullTerminatedString()
        {
            var str = BaseReader.ReadNullTerminatedString();

            // Only a read that reached past the end of the slice is out of bounds. A cursor that lands exactly on Length
            // is fine: a null-terminated field is frequently the last one in an asset, and its terminator is the slice's
            // final byte. Rejecting that made every asset whose last field was a string unreadable.
            if (Position > Length)
                throw BeyondSlice($"a null-terminated string of {str.Length} chars", Position, Length);

            return str;
        }

        public SlicedReader(IReaderProvider readerProvider, ulong start, ulong length,
            Endianness endian = Endianness.BigEndian)
        {
            BaseReader = readerProvider.CreateReader(endian);
            _offset = start;
            Length = (long)length;
            if ((long)start + Length > BaseReader.Length)
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Slice [{start}, {start + length}) does not fit in a reader of length {BaseReader.Length}.");
            BaseReader.Position = (long)_offset;
        }
    }

    public class SlicedReaderProvider : IReaderProvider
    {
        public readonly IReaderProvider BaseReaderProvider;
        public readonly ulong Offset;
        public readonly ulong Length;

        public SlicedReaderProvider(IReaderProvider readerProvider, ulong offset, ulong length)
        {
            BaseReaderProvider = readerProvider;
            Offset = offset;
            Length = length;
        }

        public IReader CreateReader(Endianness endian) => new SlicedReader(BaseReaderProvider, Offset, Length, endian);
    }
}