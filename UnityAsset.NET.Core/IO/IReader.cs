using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        public ReadOnlySpan<byte> ReadSpan(int count) => ReadBytes(count);

        public void ReadExactly(Span<byte> buffer)
        {
            var bytesRead = Read(buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
                throw new EndOfStreamException();
        }

        public bool ReadBoolean() => ReadByte() != 0;
        public sbyte ReadInt8() => (sbyte)ReadByte();
        public byte ReadUInt8() => ReadByte();
        public char ReadChar() => BitConverter.ToChar(ReadSpan(2));
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

            var ret = Encoding.UTF8.GetString(ReadSpan(length));
            Align(4);
            return ret;
        }

        public List<T> ReadList<T>(int count, Func<IReader, T> constructor)
        {
            ArrayBounds.Count(this, count, "list");
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(constructor(this));
            return list;
        }

        public List<T> ReadList<T>(Func<IReader, T> constructor) => ReadList(ReadInt32(), constructor);

        public List<T> ReadListWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
        {
            ArrayBounds.Count(this, count, "list");
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
            ArrayBounds.Count(this, count, "vector");
            var array = new T[count];
            for (int i = 0; i < count; i++)
                array[i] = constructor(this);
            return array;
        }

        public T[] ReadArray<T>(Func<IReader, T> constructor) => ReadArray(ReadInt32(), constructor);

        public T[] ReadArrayWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
        {
            ArrayBounds.Count(this, count, "vector");
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

        /// <summary>
        /// Reads a vector of unmanaged elements that are stored back to back.
        /// </summary>
        public T[] ReadArrayBlock<T>(int count) where T : unmanaged
        {
            var elementSize = Unsafe.SizeOf<T>();
            ArrayBounds.Elements(this, count, elementSize, "vector");
            if (count == 0)
                return Array.Empty<T>();

            var byteCount = (long)count * elementSize;
            if (byteCount > int.MaxValue)
                throw new InvalidDataException(
                    $"A vector of {count} × {elementSize} byte(s) is {byteCount} bytes, more than one read can span.");

            var bytes = ReadSpan((int)byteCount);
            var values = MemoryMarshal.Cast<byte, T>(bytes).ToArray();

            // A short read must fail the way the element-by-element path does, instead of silently returning a
            // truncated vector and shifting every following field.
            if (values.Length != count)
                throw new EndOfStreamException(
                    $"A vector of {count} elements needs {byteCount} bytes, but only {bytes.Length} are left.");

            if ((Endian == Endianness.BigEndian) != !BitConverter.IsLittleEndian)
                ElementSwap.Reverse(values.AsSpan(), elementSize);

            return values;
        }

        public T[] ReadArrayBlock<T>() where T : unmanaged => ReadArrayBlock<T>(ReadInt32());

        /// <summary>
        /// Reads a map as an array of pairs, keeping the loop inside the reader. The element-by-element path pays an
        /// outer delegate call, a <see cref="ReadPairWithAlign{TK, TV}"/> call and two element delegates per pair.
        /// </summary>
        public (TK, TV)[] ReadPairArrayWithAlign<TK, TV>(int count, Func<IReader, TK> keyReader,
            Func<IReader, TV> valueReader, bool keyRequiresAlign, bool valueRequiresAlign, bool pairRequiresAlign)
        {
            ArrayBounds.Count(this, count, "map");

            var pairs = new (TK, TV)[count];
            for (var index = 0; index < count; index++)
            {
                var key = keyReader(this);
                if (keyRequiresAlign)
                    Align(4);
                var value = valueReader(this);
                if (valueRequiresAlign)
                    Align(4);
                pairs[index] = (key, value);
                if (pairRequiresAlign)
                    Align(4);
            }

            return pairs;
        }

        public (TK, TV)[] ReadPairArrayWithAlign<TK, TV>(Func<IReader, TK> keyReader,
            Func<IReader, TV> valueReader, bool keyRequiresAlign, bool valueRequiresAlign, bool pairRequiresAlign) =>
            ReadPairArrayWithAlign(ReadInt32(), keyReader, valueReader, keyRequiresAlign, valueRequiresAlign,
                pairRequiresAlign);

        /// <summary>
        /// The most common Unity map shape: a sized-string key and a value. Reading the key inline instead of through a
        /// delegate removes one indirect call per entry, which is what maps of texture properties and material
        /// parameters cost otherwise.
        /// </summary>
        public (string, TV)[] ReadStringKeyedPairArrayWithAlign<TV>(int count, Func<IReader, TV> valueReader,
            bool valueRequiresAlign, bool pairRequiresAlign)
        {
            ArrayBounds.Elements(this, count, 4, "map");

            var pairs = new (string, TV)[count];
            for (var index = 0; index < count; index++)
            {
                var key = ReadSizedString();
                var value = valueReader(this);
                if (valueRequiresAlign)
                    Align(4);
                pairs[index] = (key, value);
                if (pairRequiresAlign)
                    Align(4);
            }

            return pairs;
        }

        public (string, TV)[] ReadStringKeyedPairArrayWithAlign<TV>(Func<IReader, TV> valueReader,
            bool valueRequiresAlign, bool pairRequiresAlign) =>
            ReadStringKeyedPairArrayWithAlign(ReadInt32(), valueReader, valueRequiresAlign, pairRequiresAlign);

        /// <summary>
        /// A vector of length-prefixed strings, read without a delegate call per element.
        /// </summary>
        public string[] ReadSizedStringArray(int count, bool requiresAlign)
        {
            // Every element carries at least its four-byte length prefix.
            ArrayBounds.Elements(this, count, 4, "string vector");

            var values = new string[count];
            for (var index = 0; index < count; index++)
            {
                values[index] = ReadSizedString();
                if (requiresAlign)
                    Align(4);
            }

            return values;
        }

        public string[] ReadSizedStringArray(bool requiresAlign) => ReadSizedStringArray(ReadInt32(), requiresAlign);

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

    /// <summary>
    /// Byte-order reversal for a block of equally sized elements, shared by the block read and block write paths so the
    /// two cannot drift apart.
    /// </summary>
    internal static class ElementSwap
    {
        public static void Reverse<T>(Span<T> values, int elementSize) where T : unmanaged
        {
            var bytes = MemoryMarshal.AsBytes(values);
            for (var offset = 0; offset < bytes.Length; offset += elementSize)
                for (int left = offset, right = offset + elementSize - 1; left < right; left++, right--)
                    (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
        }
    }
}