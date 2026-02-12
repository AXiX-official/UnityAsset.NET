using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

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
    private void WriteBytes(byte value, ulong count)
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
        WriteInt32(value.Length);
        WriteBytes(Encoding.UTF8.GetBytes(value));
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
}