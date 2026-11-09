using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityAsset.NET.IO
{
    public interface IWriter : ISeek
    {
        # region ISeek

        public new void Align(uint alignment)
        {
            var offset = Position % alignment;
            if (offset != 0)
            {
                var step = alignment - offset;
                WriteBytes(0, (ulong)step);
            }
        }

        # endregion

        public Endianness Endian { get; set; }
        private bool IsLittleEndian => Endian == Endianness.LittleEndian;
        private bool NeedReverse => IsLittleEndian != BitConverter.IsLittleEndian;

        public void WriteByte(byte value);
        public void WriteSByte(sbyte value) => WriteByte((byte)value);
        public void WriteBytes(ReadOnlySpan<byte> bytes);
        public ulong WriteBytes(IReader reader);
        public void WriteBytes(byte value, ulong count)
        {
            for (ulong i = 0; i < count; i++)
                WriteByte(value);
        }
        public void WriteBoolean(bool value) => WriteByte((byte)(value ? 1 : 0));
        public void WriteInt8(sbyte value) => WriteSByte(value);
        public void WriteUInt8(byte value) => WriteByte(value);
        public void WriteChar(char value) => WriteBytes(BitConverter.GetBytes(value).AsSpan(0, 2));
        public void WriteInt16(short value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteUInt16(ushort value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteInt32(int value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteUInt32(uint value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteInt64(long value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteUInt64(ulong value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(value)));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteSingle(float value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(BitConverter.SingleToInt32Bits(value))));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteDouble(double value)
        {
            if (NeedReverse)
                WriteBytes(BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value))));
            else
                WriteBytes(BitConverter.GetBytes(value));
        }
        public void WriteNullTerminatedString(string value)
        {
            WriteBytes(Encoding.UTF8.GetBytes(value));
            WriteByte(0);
        }
        public void WriteSizedString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(bytes.Length);
            WriteBytes(bytes);
            Align(4);
        }
        public void WriteList<T>(int count, List<T> list, Action<IWriter, T> writer)
        {
            WriteInt32(count);
            foreach (var item in list)
                writer(this, item);
        }
        public void WriteList<T>(List<T> list, Action<IWriter, T> writer) => WriteList(list.Count, list, writer);
        public void WriteListWithAlign<T>(int count, List<T> list, Action<IWriter, T> writer, bool requiresAlign)
        {
            WriteInt32(count);
            foreach (var item in list)
            {
                writer(this, item);
                if (requiresAlign)
                    Align(4);
            }
        }
        public void WriteListWithAlign<T>(List<T> list, Action<IWriter, T> writer, bool requiresAlign) =>
            WriteListWithAlign(list.Count, list, writer, requiresAlign);
        public void WriteArray<T>(T[] array, Action<IWriter, T> writer)
        {
            foreach (var item in array)
                writer(this, item);
        }
        public void WriteArray<T>(int count, T[] array, Action<IWriter, T> writer)
        {
            WriteInt32(count);
            foreach (var item in array)
                writer(this, item);
        }
        public void WriteArrayWithCount<T>(T[] array, Action<IWriter, T> writer) => WriteArray(array.Length, array, writer);
        public void WriteArrayWithAlign<T>(int count, T[] array, Action<IWriter, T> writer, bool requiresAlign)
        {
            WriteInt32(count);
            foreach (var item in array)
            {
                writer(this, item);
                if (requiresAlign)
                    Align(4);
            }
        }
        public void WriteArrayWithAlign<T>(T[] array, Action<IWriter, T> writer, bool requiresAlign) =>
            WriteArrayWithAlign(array.Length, array, writer, requiresAlign);

        /// <summary>
        /// Writes a vector of unmanaged elements as one block, the counterpart of
        /// <see cref="IReader.ReadArrayBlock{T}(int)"/>. The element-by-element path pays a delegate call and an
        /// interface call per item, which is what made large buffers dominate the runtime-codegen backend on write too.
        /// </summary>
        /// <remarks>
        /// Callers must only choose this overload when the elements need no padding of their own.
        /// </remarks>
        public void WriteArrayBlock<T>(T[] values) where T : unmanaged
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));
            WriteInt32(values.Length);
            WriteArrayBlockValues(values);
        }

        public void WriteArrayBlockValues<T>(T[] values) where T : unmanaged
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length == 0)
                return;

            // One copy when the bytes are already in the file's order.
            if (!NeedReverse)
            {
                WriteBytes(MemoryMarshal.AsBytes(values.AsSpan()));
                return;
            }

            var copy = values.ToArray();
            ElementSwap.Reverse(copy.AsSpan(), Unsafe.SizeOf<T>());
            WriteBytes(MemoryMarshal.AsBytes(copy.AsSpan()));
        }

        /// <summary>
        /// Writes a vector of length-prefixed strings, the counterpart of <see cref="IReader.ReadSizedStringArray"/>. The
        /// loop stays here so no delegate runs per element.
        /// </summary>
        public void WriteSizedStringArray(string[] values, bool requiresAlign)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));
            WriteInt32(values.Length);
            foreach (var value in values)
            {
                WriteSizedString(value);
                if (requiresAlign)
                    Align(4);
            }
        }

        /// <summary>
        /// Writes a map as an array of pairs, the counterpart of <see cref="IReader.ReadPairArrayWithAlign{TK, TV}"/>.
        /// </summary>
        public void WritePairArrayWithAlign<TK, TV>((TK, TV)[] pairs, Action<IWriter, TK> keyWriter,
            Action<IWriter, TV> valueWriter, bool keyRequiresAlign, bool valueRequiresAlign, bool pairRequiresAlign)
        {
            if (pairs is null)
                throw new ArgumentNullException(nameof(pairs));
            WriteInt32(pairs.Length);
            foreach (var (key, value) in pairs)
            {
                keyWriter(this, key);
                if (keyRequiresAlign)
                    Align(4);
                valueWriter(this, value);
                if (valueRequiresAlign)
                    Align(4);
                if (pairRequiresAlign)
                    Align(4);
            }
        }

        /// <summary>
        /// Writes a string-keyed map, the counterpart of <see cref="IReader.ReadStringKeyedPairArrayWithAlign{TV}"/>.
        /// Writing the key inline removes one delegate call per entry.
        /// </summary>
        public void WriteStringKeyedPairArrayWithAlign<TV>((string, TV)[] pairs, Action<IWriter, TV> valueWriter,
            bool valueRequiresAlign, bool pairRequiresAlign)
        {
            if (pairs is null)
                throw new ArgumentNullException(nameof(pairs));
            WriteInt32(pairs.Length);
            foreach (var (key, value) in pairs)
            {
                WriteSizedString(key);
                valueWriter(this, value);
                if (valueRequiresAlign)
                    Align(4);
                if (pairRequiresAlign)
                    Align(4);
            }
        }
        public void WritePairWithAlign<TK, TV>((TK, TV) value, Action<IWriter, TK> keyWriter,
            Action<IWriter, TV> valueWriter, bool keyRequiresAlign, bool valueRequiresAlign) where TK : notnull
        {
            keyWriter(this, value.Item1);
            if (keyRequiresAlign)
                Align(4);
            valueWriter(this, value.Item2);
            if (valueRequiresAlign)
                Align(4);
        }
        public void WriteFixedArray<T>(T[] array, Action<IWriter, T> writer)
        {
            foreach (var item in array)
                writer(this, item);
        }

        public void WriteStream(Stream stream);
    }
}