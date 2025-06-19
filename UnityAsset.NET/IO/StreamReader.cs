using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;

namespace UnityAsset.NET.IO;

public class StreamReader : IReader, IDisposable
{
    private readonly Stream _stream;
    private Endianness _endian;
    private readonly bool _leaveOpen;

    public StreamReader(Stream stream, Endianness endian = Endianness.BigEndian, bool leaveOpen = false)
    {
        _stream = stream;
        _endian = endian;
        _leaveOpen = leaveOpen;
    }

    public long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }
    public void Seek(long offset) => Position = offset;
    public void Advance(long count) => Seek(Position + count);
    public void Rewind(long count) => Seek(Position - count);
    public void Align(int alignment)
    {
        var offset = Position % alignment;
        if (offset != 0)
            Advance(alignment - offset);
    }

    public Endianness Endian => _endian;

    public IReader CastEndian(Endianness endian)
    {
        _endian = endian;
        return this;
    }
    
    public long Length => _stream.Length;

    public ReadOnlySpan<byte> ReadOnlySlice(long start, int length)
    {
        var pos = Position;
        Seek(start);
        byte[] data = new byte[length];
        _stream.ReadExactly(data, 0, length);
        Seek(pos);
        return data;
    }

    public byte ReadByte()
    {
        return (byte)_stream.ReadByte();
    }

    public byte[] ReadBytes(int count)
    {
        byte[] bytes = new byte[count];
        _stream.ReadExactly(bytes, 0, count);
        return bytes;
    }

    public ReadOnlySpan<byte> ReadOnlySpanBytes(int count) => ReadBytes(count);
    public Boolean ReadBoolean() => ReadByte() != 0;
    public sbyte ReadInt8() => (sbyte)ReadByte();
    public byte ReadUInt8() => ReadByte();

    public Int16 ReadInt16()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt16BigEndian(ReadOnlySpanBytes(2))
            : BinaryPrimitives.ReadInt16LittleEndian(ReadOnlySpanBytes(2));
    }

    public UInt16 ReadUInt16()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt16BigEndian(ReadOnlySpanBytes(2))
            : BinaryPrimitives.ReadUInt16LittleEndian(ReadOnlySpanBytes(2));
    }

    public Int32 ReadInt32()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadInt32LittleEndian(ReadOnlySpanBytes(4));
    }

    public UInt32 ReadUInt32()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt32BigEndian(ReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadUInt32LittleEndian(ReadOnlySpanBytes(4));
    }

    public Int64 ReadInt64()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt64BigEndian(ReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadInt64LittleEndian(ReadOnlySpanBytes(8));
    }

    public UInt64 ReadUInt64()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt64BigEndian(ReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadUInt64LittleEndian(ReadOnlySpanBytes(8));
    }

    public float ReadFloat()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadSingleBigEndian(ReadOnlySpanBytes(4))
            : BinaryPrimitives.ReadSingleLittleEndian(ReadOnlySpanBytes(4));
    }

    public double ReadDouble()
    {
        return _endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadDoubleBigEndian(ReadOnlySpanBytes(8))
            : BinaryPrimitives.ReadDoubleLittleEndian(ReadOnlySpanBytes(8));
    }

    public string ReadNullTerminatedString()
    {
        using var ms = new MemoryStream();
        byte b;
        while ((b = ReadByte()) != 0)
        {
            ms.WriteByte(b);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public string ReadSizedString()
    {
        var length = ReadInt32();
        return length > 0 ? Encoding.UTF8.GetString(((IReader)this).ReadOnlySpanBytes(length)) : "";
    }
    
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream?.Dispose();
        }
        //GC.SuppressFinalize(this);
    }
}