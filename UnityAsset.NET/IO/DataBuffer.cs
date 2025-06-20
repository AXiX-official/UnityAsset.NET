﻿using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public class DataBuffer : IFile
{
    private Memory<byte> _data;
    private int _position;
    private int _size;
    private bool _bigEndian;
    public bool CanGrow;
    
    public int Position => _position;
    public int Length => _size;
    public int Capacity => _data.Length;

    public bool IsBigEndian
    {
        get => _bigEndian;
        set => _bigEndian = value;
    }

    public DataBuffer(Memory<byte> data, bool bigEndian = true, bool canGrow = true)
    {
        _data = data;
        _position = 0;
        _size = data.Length;
        _bigEndian = bigEndian;
        CanGrow = canGrow;
    }
    
    public DataBuffer(int size, bool bigEndian = true, bool canGrow = true)
    {
        _data = new byte[size];
        _position = 0;
        _size = 0;
        _bigEndian = bigEndian;
        CanGrow = canGrow;
    }
    
    public Span<byte> AsSpan() => _data.Span;
    public Memory<byte> AsMemory() => _data;
    public  Span<byte> Slice(int start, int size) => AsSpan().Slice(start, size);
    public  Span<byte> SliceForward(int size) => AsSpan().Slice(_position, size);
    public Span<byte> SliceForward() => AsSpan().Slice(_position);
    public Span<byte> ReadSpanBytes(int count)
    {
        var span = SliceForward(count);
        Advance(count);
        return span;
    }
    public void Advance(int count)
    {
        _position += count;
        if (_position > _data.Length)
        {
            throw new IndexOutOfRangeException();
        }
        if (_position > _size)
        {
            _size = _position;
        }
    }
    public void Rewind(int count)
    { 
        _position -= count;
        if (_position < 0)
        {
            throw new IndexOutOfRangeException();
        }
    }
    public void Seek(int offset)
    {
        if (offset < 0 || offset > _data.Length)
        {
            throw new IndexOutOfRangeException();
        }
        _position = offset;
        if (_position > _size)
        {
            _size = _position;
        }
    }
    
    public void EnsureCapacity(int requiredSize)
    {
        if (_position + requiredSize > Capacity)
        {
            if (CanGrow)
            {
                int newCapacity = Math.Max(_data.Length * 2, _position + requiredSize);
                var newData = new byte[newCapacity];
                _data.Slice(0, _size).CopyTo(newData);
                _data = newData;
            }
            else
            {
                if (!CanGrow) throw new InvalidOperationException("Sliced buffer cannot grow");
            }
        }
    }
    
    public DataBuffer SliceBuffer(int start, int length)
    {
        if (start < 0 || start + length > _data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The specified range is out of bounds.");
        }

        return new DataBuffer(_data.Slice(start, length), _bigEndian, false)
        {
            _position = 0,
            _size = length,
        };
    }
    
    public DataBuffer SliceBuffer(int length)
    {
        if (_position + length > _data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The specified range is out of bounds.");
        }

        return new DataBuffer(_data.Slice(_position, length), _bigEndian, false)
        {
            _position = 0,
            _size = length,
        };
    }
    
    public DataBuffer SliceBufferToEnd()
    {
        int length = _data.Length - _position;
        return SliceBuffer(length);
    }
    
    public static DataBuffer FromFile(string filePath, bool bigEndian = true)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        return new DataBuffer(fileData, bigEndian);
    }
    
    public static DataBuffer FromFileStream(string filePath, bool bigEndian = true)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        byte[] buffer = new byte[fs.Length];
        fs.ReadExactly(buffer, 0, buffer.Length);
        return new DataBuffer(buffer, bigEndian);
    }
    
    public void WriteToFile(string filePath, int size)
    {
        byte[] data = AsMemory().Slice(0, size).ToArray();
        File.WriteAllBytes(filePath, data);
    }
    
    public int Align(int alignment)
    {
        var offset = Position % alignment;
        if (offset != 0)
        {
            EnsureCapacity(alignment - offset);
            Advance(alignment - offset);
            return alignment - offset;
        }

        return 0;
    }

    public Boolean ReadBoolean()
    {
        var span = ReadSpanBytes(1);
        return span[0] != 0;
    }
    
    public int WriteBoolean(Boolean value)
    {
        EnsureCapacity(1);
        var span = ReadSpanBytes(1);
        span[0] = value ? (byte)1 : (byte)0;
        return 1;
    }
    
    public sbyte ReadInt8()
    {
        return (sbyte)ReadByte();
    }
    
    public int WriteInt8(sbyte value)
    {
        WriteByte((byte)value);
        return 1;
    }
    
    public byte ReadUInt8()
    {
        return ReadByte();
    }
    
    public int WriteUInt8(byte value)
    {
        WriteByte(value);
        return 1;
    }

    public Int16 ReadInt16()
    {
        var span = ReadSpanBytes(2);
        return IsBigEndian ? BinaryPrimitives.ReadInt16BigEndian(span) : BinaryPrimitives.ReadInt16LittleEndian(span);
    }
    
    public int WriteInt16(Int16 value)
    {
        EnsureCapacity(2);
        var span = ReadSpanBytes(2);
        if (IsBigEndian)
            BinaryPrimitives.WriteInt16BigEndian(span, value);
        else
            BinaryPrimitives.WriteInt16LittleEndian(span, value);
        return 2;
    }
    
    public UInt16 ReadUInt16()
    {
        var span = ReadSpanBytes(2);
        return IsBigEndian ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt16LittleEndian(span);
    }
    
    public int WriteUInt16(UInt16 value)
    {
        EnsureCapacity(2);
        var span = ReadSpanBytes(2);
        if (IsBigEndian)
            BinaryPrimitives.WriteUInt16BigEndian(span, value);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        return 2;
    }
    
    public Int32 ReadInt32()
    {
        var span = ReadSpanBytes(4); 
        return IsBigEndian ? BinaryPrimitives.ReadInt32BigEndian(span) : BinaryPrimitives.ReadInt32LittleEndian(span);
    }
    
    public int WriteInt32(Int32 value)
    {
        EnsureCapacity(4);
        var span = ReadSpanBytes(4);
        if (IsBigEndian)
            BinaryPrimitives.WriteInt32BigEndian(span, value);
        else
            BinaryPrimitives.WriteInt32LittleEndian(span, value);
        return 4;
    }
    
    public UInt32 ReadUInt32()
    {
        var span = ReadSpanBytes(4);
        return IsBigEndian ? BinaryPrimitives.ReadUInt32BigEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }
    
    public int WriteUInt32(UInt32 value)
    {
        EnsureCapacity(4);
        var span = ReadSpanBytes(4);
        if (IsBigEndian)
            BinaryPrimitives.WriteUInt32BigEndian(span, value);
        else
            BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        return 4;
    }
    
    public Int64 ReadInt64()
    {
        var span = ReadSpanBytes(8);
        return IsBigEndian ? BinaryPrimitives.ReadInt64BigEndian(span) : BinaryPrimitives.ReadInt64LittleEndian(span);
    }
    
    public int WriteInt64(Int64 value)
    {
        EnsureCapacity(8);
        var span = ReadSpanBytes(8);
        if (IsBigEndian)
            BinaryPrimitives.WriteInt64BigEndian(span, value);
        else
            BinaryPrimitives.WriteInt64LittleEndian(span, value);
        return 8;
    }
    
    public UInt64 ReadUInt64()
    {
        var span = ReadSpanBytes(8);
        return IsBigEndian ? BinaryPrimitives.ReadUInt64BigEndian(span) : BinaryPrimitives.ReadUInt64LittleEndian(span);
    }
    
    public int WriteUInt64(UInt64 value)
    {
        EnsureCapacity(8);
        var span = ReadSpanBytes(8);
        if (IsBigEndian)
            BinaryPrimitives.WriteUInt64BigEndian(span, value);
        else
            BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        return 8;
    }
    
    public float ReadFloat()
    {
        var span = ReadSpanBytes(4);
        return IsBigEndian ? BinaryPrimitives.ReadSingleBigEndian(span) : BinaryPrimitives.ReadSingleLittleEndian(span);
    }
    
    public int WriteFloat(float value)
    {
        EnsureCapacity(4);
        var span = ReadSpanBytes(4);
        if (IsBigEndian)
            BinaryPrimitives.WriteSingleBigEndian(span, value);
        else
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
        return 4;
    }
    
    public double ReadDouble()
    {
        var span = ReadSpanBytes(8);
        return IsBigEndian ? BinaryPrimitives.ReadDoubleBigEndian(span) : BinaryPrimitives.ReadDoubleLittleEndian(span);
    }
    
    public int WriteDouble(double value)
    {
        EnsureCapacity(8);
        var span = ReadSpanBytes(8);
        if (IsBigEndian)
            BinaryPrimitives.WriteDoubleBigEndian(span, value);
        else
            BinaryPrimitives.WriteDoubleLittleEndian(span, value);
        return 8;
    }
    
    public string ReadNullTerminatedString(int maxLength = 32767)
    {
        var span = SliceForward();
        int nullTerminator = span.IndexOf((byte)0);
        if (nullTerminator < 0 || nullTerminator > maxLength)
            nullTerminator = maxLength;
        var strBytes = span.Slice(0, nullTerminator);
        Advance(nullTerminator + 1);
        return Encoding.UTF8.GetString(strBytes);
    }
    
    public int WriteNullTerminatedString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        EnsureCapacity(byteCount + 1);
        var span = ReadSpanBytes(byteCount + 1);
        Encoding.UTF8.GetBytes(value, span);
        span[byteCount] = 0;
        return byteCount + 1;
    }
    
    public string ReadSizedString()
    {
        var result = "";
        var length = ReadInt32();
        if (length > 0)
        {
            var stringData = ReadBytes(length);
            result = Encoding.UTF8.GetString(stringData);
        }
        return result;
    }
    
    public int WriteSizedString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        EnsureCapacity(byteCount + 8);
        WriteInt32(byteCount);
        var span = ReadSpanBytes(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        Align(4);
        return 4 + byteCount;
    }

    public List<T> ReadList<T>(int count, Func<DataBuffer, T> constructor)
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(constructor(this));
        }
        return list;
    }
    
    public int WriteList<T>(List<T> array, Func<DataBuffer, T, int> writer)
    {
        int size = 0;
        foreach (var item in array.AsReadOnlySpan())
        {
            size += writer(this, item);
        }
        return size;
    }
    
    public int WriteListWithCount<T>(List<T> array, Func<DataBuffer, T, int> writer)
    {
        int size = 0;
        size += WriteInt32(array.Count);
        foreach (var item in array.AsReadOnlySpan())
        {
            size += writer(this, item);
        }
        return size;
    }

    public byte ReadByte()
    {
        return ReadSpanBytes(1)[0];
    }
    
    public int WriteByte(byte b)
    {
        EnsureCapacity(1);
        var span = ReadSpanBytes(1);
        span[0] = b;
        return 1;
    }

    public byte[] ReadBytes(int count)
    {
        return ReadSpanBytes(count).ToArray();
    }

    public int WriteBytes(byte[] data)
    {
        var len = data.Length;
        EnsureCapacity(len);
        data.AsSpan().CopyTo(ReadSpanBytes(len));
        return len;
    }
    
    public int WriteBytes(Span<byte> data)
    {
        var len = data.Length;
        EnsureCapacity(len);
        data.CopyTo(ReadSpanBytes(len));
        return len;
    }
    
    public int[] ReadIntArray(int count)
    {
        int[] array = new int[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = ReadInt32();
        }
        return array;
    }
    
    public int WriteIntArrayWithCount(int[] array)
    {
        WriteInt32(array.Length);
        foreach (var item in array.AsSpan())
        {
            WriteInt32(item);
        }
        return 4 + 4 * array.Length;
    }
}