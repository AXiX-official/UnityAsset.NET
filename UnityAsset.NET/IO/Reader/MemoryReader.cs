using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class MemoryReader : IReader
{
    private readonly Memory<byte> _data;
    private int _position;
    private int _length;

    public MemoryReader(byte[] data, int position = 0, Endianness endian = Endianness.BigEndian)
    {
        _data = data;
        _position = position;
        _length = data.Length;
        Endian = endian;
    }
    public MemoryReader(Memory<byte> data, int position = 0, Endianness endian = Endianness.BigEndian)
    {
        _data = data;
        _position = position;
        _length = data.Length;
        Endian = endian;
    }
    // TEMPORARY: make it writable
    public MemoryReader(int capacity = 0, Endianness endian = Endianness.BigEndian)
    {
        _data = new byte[capacity];
        _position = 0;
        _length = capacity;
        Endian = endian;
    }
    
    public ReadOnlySpan<byte> AsReadOnlySpan => _data.Span;
    
    # region ISeek
    public long Position
    {
        get => _position;
        set => _position = (int)value;
    }
    public long Length => _length;
    # endregion
    
    # region IReader
    public Endianness Endian { get; set; }
    
    public byte ReadByte()
    {
        var b = _data.Span[_position];
        _position++;
        return b;
    }
    private ReadOnlySpan<byte> ReadReadOnlySpanBytes(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var span = _data.Span.Slice(_position, count);
        _position += count;
        return span;
    }
    public byte[] ReadBytes(int count) => ReadReadOnlySpanBytes(count).ToArray();

    public void ReadExactly(Span<byte> buffer)
    {
        var span = ReadReadOnlySpanBytes(buffer.Length);
        span.CopyTo(buffer);
    }
    public Int16 ReadInt16()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt16BigEndian(ReadReadOnlySpanBytes(2))
            : BinaryPrimitives.ReadInt16LittleEndian(ReadReadOnlySpanBytes(2));
    }
    public UInt16 ReadUInt16()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt16BigEndian(ReadReadOnlySpanBytes(2))
            : BinaryPrimitives.ReadUInt16LittleEndian(ReadReadOnlySpanBytes(2));
    }
    public Int32 ReadInt32()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt32BigEndian(ReadReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadInt32LittleEndian(ReadReadOnlySpanBytes(4));
    }
    public UInt32 ReadUInt32()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt32BigEndian(ReadReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadUInt32LittleEndian(ReadReadOnlySpanBytes(4));
    }
    public Int64 ReadInt64()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt64BigEndian(ReadReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadInt64LittleEndian(ReadReadOnlySpanBytes(8));
    }
    public UInt64 ReadUInt64()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt64BigEndian(ReadReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadUInt64LittleEndian(ReadReadOnlySpanBytes(8));
    }
    public float ReadSingle()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadSingleBigEndian(ReadReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadSingleLittleEndian(ReadReadOnlySpanBytes(4));
    }
    public double ReadDouble()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadDoubleBigEndian(ReadReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadDoubleLittleEndian(ReadReadOnlySpanBytes(8));
    }
    public string ReadNullTerminatedString()
    {
        var span = _data.Span.Slice(_position, _length - _position);
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