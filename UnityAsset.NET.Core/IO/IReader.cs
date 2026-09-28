using System.Text;

namespace UnityAsset.NET.IO
{
    public interface IReader : ISeek
    {
        # region ISeek

        /// <summary>
        /// Aligns the current position to the specified alignment.
        /// If the current position is already aligned, no action is taken.
        /// If the alignment is greater than the remaining bytes, the position is set to the end of the stream.
        /// </summary>
        /// <param name="alignment"></param>
        public new void Align(uint alignment)
        {
            if (alignment <= 0)
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be greater than 0.");
            var offset = Position % alignment;
            if (offset != 0)
            {
                var step = alignment - offset;
                if (step >= Remaining)
                {
                    Seek(0, SeekOrigin.End);
                }
                else
                {
                    Seek(alignment - offset, SeekOrigin.Current);
                }
            }
        }

        # endregion

        public Endianness Endian { get; set; }
        public long Remaining => Length - Position;
        /// <summary>
        /// If remaining bytes is enough, read count bytes into buffer starting from offset, and return the number of bytes read.
        /// Otherwise, read as many bytes as possible into buffer starting from offset, and return the number of bytes read.
        /// If the buffer is not large enough to hold the requested number of bytes, throw an exception.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> is outside <paramref name="buffer"/>, <paramref name="count"/> is negative, or
        /// <paramref name="buffer"/> is too small to hold the requested bytes. Nothing is consumed in that case.
        /// </exception>
        public int Read(Span<byte> buffer, int offset, int count);
        public byte ReadByte();
        public sbyte ReadSByte() => (sbyte)ReadByte();
        public byte[] ReadBytes(int count);

        public void ReadExactly(Span<byte> buffer)
        {
            var bytesRead = Read(buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
                throw new EndOfStreamException();
        }

        public bool ReadBoolean() => ReadByte() != 0;
        public sbyte ReadInt8() => (sbyte)ReadByte();
        public byte ReadUInt8() => ReadByte();
        public char ReadChar() => BitConverter.ToChar(ReadBytes(2), 0);
        public short ReadInt16();
        public ushort ReadUInt16();
        public int ReadInt32();
        public uint ReadUInt32();
        public long ReadInt64();
        public ulong ReadUInt64();
        public float ReadSingle();
        public double ReadDouble();
        public string ReadNullTerminatedString();

        /// <summary>
        /// Reads a UTF-8 string whose byte count is stored in a four-byte prefix, then aligns to four.
        /// </summary>
        /// <exception cref="EndOfStreamException">
        /// The prefix is negative, or claims more bytes than the reader holds. Returning an empty string for those used to
        /// make a corrupt length read as an empty field while the cursor still moved past the prefix, so every field after
        /// it was read from the wrong place — a silent shift is worse than a failed read.
        /// </exception>
        public string ReadSizedString()
        {
            var length = ReadInt32();
            if (length < 0 || length > Remaining)
                throw new EndOfStreamException(
                    $"A size prefix of {length} bytes cannot be read with {Remaining} bytes left.");

            if (length == 0)
            {
                Align(4);
                return string.Empty;
            }

            var ret = Encoding.UTF8.GetString(ReadBytes(length));
            Align(4);
            return ret;
        }

        public List<T> ReadList<T>(int count, Func<IReader, T> constructor)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(constructor(this));
            return list;
        }

        public List<T> ReadList<T>(Func<IReader, T> constructor) => ReadList(ReadInt32(), constructor);

        public List<T> ReadListWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(constructor(this));
                if (requiresAlign)
                    Align(4);
            }

            return list;
        }

        public List<T> ReadListWithAlign<T>(Func<IReader, T> constructor, bool requiresAlign) =>
            ReadListWithAlign(ReadInt32(), constructor, requiresAlign);

        public T[] ReadArray<T>(int count, Func<IReader, T> constructor)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            var array = new T[count];
            for (int i = 0; i < count; i++)
                array[i] = constructor(this);
            return array;
        }

        public T[] ReadArray<T>(Func<IReader, T> constructor) => ReadArray(ReadInt32(), constructor);

        public T[] ReadArrayWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            var array = new T[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = constructor(this);
                if (requiresAlign)
                    Align(4);
            }

            return array;
        }

        public T[] ReadArrayWithAlign<T>(Func<IReader, T> constructor, bool requiresAlign) =>
            ReadArrayWithAlign(ReadInt32(), constructor, requiresAlign);

        public (TK, TV) ReadPairWithAlign<TK, TV>(Func<IReader, TK> keyConstructor,
            Func<IReader, TV> valueConstructor, bool keyRequiresAlign, bool valueRequiresAlign) where TK : notnull
        {
            TK key = keyConstructor(this);
            if (keyRequiresAlign)
                Align(4);
            TV value = valueConstructor(this);
            if (valueRequiresAlign)
                Align(4);
            return (key, value);
        }

        public void ReadFixedArray<T>(in T[] array, Func<IReader, T> constructor) where T : struct
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = constructor(this);
        }

        public static void ValidCount(int length, int offset, int count, string offsetName, string countName)
        {
            if (offset < 0 || offset > length)
                throw new ArgumentOutOfRangeException(offsetName);
            if (count < 0 || offset + count > length)
                throw new ArgumentOutOfRangeException(countName);
        }
    }
}