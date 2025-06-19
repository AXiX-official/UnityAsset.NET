using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;

namespace UnityAsset.NET.IO;

public abstract class MemoryBinaryIO : IReader, IWriter
{
    protected readonly Memory<byte> Data;
    protected int Pos;
    
    public static MemoryBinaryIO Create(Memory<byte> data, int pos = 0, Endianness endian = Endianness.BigEndian)
    {
        return endian switch
        {
            Endianness.BigEndian => new BigEndianMemoryBinaryIO(data, pos),
            Endianness.LittleEndian => new LittleEndianMemoryBinaryIO(data, pos),
            _ => throw new ArgumentException($"Unsupported endianness: {endian}", nameof(endian))
        };
    }
    
    public static MemoryBinaryIO Create(int size, Endianness endian = Endianness.BigEndian)
    {
        return endian switch
        {
            Endianness.BigEndian => new BigEndianMemoryBinaryIO(size),
            Endianness.LittleEndian => new LittleEndianMemoryBinaryIO(size),
            _ => throw new ArgumentException($"Unsupported endianness: {endian}", nameof(endian))
        };
    }
    
    protected MemoryBinaryIO(Memory<byte> data, int pos = 0)
    {
        Data = data;
        Pos = pos;
    }

    protected MemoryBinaryIO(int size)
    {
        Data = new byte[size];
        Pos = 0;
    }
    public abstract MemoryBinaryIO CastEndian(Endianness endian);
    public Span<byte> AsWritableSpan => Data.Span;
    public ReadOnlySpan<byte> AsReadOnlySpan => Data.Span;
    
    # region ISeek
    public long Position
    {
        get => Pos;
        set => Pos = (int)value;
    }
    public void Seek(long offset) => Pos = (int)offset;
    public void Advance(long count) => Seek(Pos + count);
    public void Rewind(long count) => Seek(Pos - count);
    public void Align(int alignment)
    {
        var offset = Pos % alignment;
        if (offset != 0)
            Advance(alignment - offset);
    }
    # endregion
    # region IReader
    public abstract Endianness Endian { get; }
    IReader IReader.CastEndian(Endianness endian) => CastEndian(endian);
    public long Length => Data.Length;
    public ReadOnlySpan<byte> ReadOnlySlice(long start, int length) => Data.Span.Slice((int)start, length);
    public byte ReadByte()
    {
        var b = Data.Span[Pos];
        Advance(1);
        return b;
    }
    public byte[] ReadBytes(int count) => ReadOnlySpanBytes(count).ToArray();
    public ReadOnlySpan<byte> ReadOnlySpanBytes(int count) => GetWritableSpan(count);
    public abstract Int16 ReadInt16();
    public abstract UInt16 ReadUInt16();
    public abstract Int32 ReadInt32();
    public abstract UInt32 ReadUInt32();
    public abstract Int64 ReadInt64();
    public abstract UInt64 ReadUInt64();
    public abstract float ReadFloat();
    public abstract double ReadDouble();
    public string ReadNullTerminatedString()
    {
        var span = Data.Span[Pos..];
        int nullTerminator = span.IndexOf((byte)0);
        if (nullTerminator < 0)
            throw new IndexOutOfRangeException();
        var strBytes = span[..nullTerminator];
        Advance(nullTerminator + 1);
        return Encoding.UTF8.GetString(strBytes);
    }
    public string ReadSizedString()
    {
        var length = ReadInt32();
        return length > 0 ? Encoding.UTF8.GetString(((IReader)this).ReadOnlySpanBytes(length)) : "";
    }
    # endregion
    # region IWriter
    IWriter IWriter.CastEndian(Endianness endian) => CastEndian(endian);
    public Span<byte> GetWritableSpan(int count)
    {
        var span = Data.Span[Pos..(Pos + count)];
        Advance(count);
        return span;
    }
    public void WriteByte(byte b)
    {
        Data.Span[Pos] = b;
        Advance(1);
    }
    public void WriteBytes(byte[] data) => data.AsSpan().CopyTo(GetWritableSpan(data.Length));
    public void WriteBytes(Span<byte> data) => data.CopyTo(GetWritableSpan(data.Length));
    public abstract void WriteInt16(Int16 value);
    public abstract void WriteUInt16(UInt16 value);
    public abstract void WriteInt32(Int32 value);
    public abstract void WriteUInt32(UInt32 value);
    public abstract void WriteInt64(Int64 value);
    public abstract void WriteUInt64(UInt64 value);
    public abstract void WriteFloat(float value);
    public abstract void WriteDouble(double value);
    public void WriteNullTerminatedString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var span = GetWritableSpan(byteCount + 1);
        Encoding.UTF8.GetBytes(value, span);
        span[byteCount] = 0;
    }
    public void WriteSizedString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt32(byteCount);
        var span = GetWritableSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
    }
    public void WriteList<T>(List<T> array, Action<IWriter, T> writer)
    {
        foreach (var item in array.AsReadOnlySpan())
        {
            writer(this, item);
        }
    }
    public void WriteListWithCount<T>(List<T> array, Action<IWriter, T> writer)
    {
        WriteInt32(array.Count);
        foreach (var item in array.AsReadOnlySpan())
            writer(this, item);
    }
    public void WriteIntArrayWithCount(int[] array)
    {
        WriteInt32(array.Length);
        foreach (var item in array.AsSpan())
            WriteInt32(item);
    }
    # endregion
}