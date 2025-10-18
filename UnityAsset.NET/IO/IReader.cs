using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public interface IReader : ISeek, IFile
{
    public new void Align(int alignment)
    {
        var offset = Position % alignment;
        if (offset != 0)
        {
            if (Position + alignment - offset >= Length)
            {
                Seek(Length - 1);
            }
            else
            {
                Seek(Position + alignment - offset);
            }
        }
    }
    public Endianness Endian { get; set; }
    public long Length { get; }
    public byte ReadByte();
    public byte[] ReadBytes(int count);
    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesAvailable = (int)Math.Min(count, Length - Position);
        var bytesRead = ReadBytes(bytesAvailable);
        Array.Copy(bytesRead, 0, buffer, offset, bytesAvailable);
        return bytesAvailable;
    }
    public Boolean ReadBoolean() => ReadByte() != 0;
    public sbyte ReadInt8() => (sbyte)ReadByte();
    public byte ReadUInt8() => ReadByte();
    public Int16 ReadInt16()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt16BigEndian(ReadBytes(2))
            : BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));
    }
    public UInt16 ReadUInt16()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2))
            : BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));
    }
    public uint ReverseInt(uint value)
    {
        value = (value >> 16) | (value << 16);
        return ((value & 0xFF00FF00) >> 8) | ((value & 0x00FF00FF) << 8);
    }
    public uint ReadUInt24()
    {
        unchecked
        {
            return Endian == Endianness.BigEndian 
                ? ReverseInt(BitConverter.ToUInt32(new byte[] { 0 }.Concat(ReadBytes(3)).ToArray(), 0)) 
                : BitConverter.ToUInt32(ReadBytes(3).Concat(new byte[] { 0 }).ToArray(), 0);
        }
    }
    public Int32 ReadInt32()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt32BigEndian(ReadBytes(4))
            : BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));
    }
    public UInt32 ReadUInt32()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4))
            : BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
    }
    public Int64 ReadInt64()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadInt64BigEndian(ReadBytes(8))
            : BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(8));
    }
    public UInt64 ReadUInt64()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(8))
            : BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
    }
    public float ReadFloat()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadSingleBigEndian(ReadBytes(4))
            : BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(4));
    }
    public double ReadDouble()
    {
        return Endian == Endianness.BigEndian 
            ? BinaryPrimitives.ReadDoubleBigEndian(ReadBytes(8))
            : BinaryPrimitives.ReadDoubleLittleEndian(ReadBytes(8));
    }
    public string ReadNullTerminatedString();
    public string ReadSizedString()
    {
        var length = ReadInt32();
        if (length > Length - Position || length < 0)
            // Fuck Unity
            return String.Empty;
        var ret = length > 0 ? Encoding.UTF8.GetString(ReadBytes(length)) : String.Empty;
        return ret;
    }
    public int[] ReadIntArray(int count)
    {
        int[] array = new int[count];
        var arraySpan = array.AsSpan();
        for (int i = 0; i < count; i++)
            arraySpan[i] = ReadInt32();
        return array;
    }
    public List<T> ReadList<T>(int count, Func<IReader, T> constructor)
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(constructor(this));
        return list;
    }
    public List<T> ReadListWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(constructor(this));
            if (requiresAlign) 
                Align(4);
        }
        return list;
    }
    
    public KeyValuePair<TK, TV> ReadPairWithAlign<TK, TV>(Func<IReader, TK> keyConstructor,
        Func<IReader, TV> valueConstructor, bool keyRequiresAlign, bool valueRequiresAlign) where TK : notnull
    {
        TK key = keyConstructor(this);
        if (keyRequiresAlign) 
            Align(4);
        TV value = valueConstructor(this);
        if (valueRequiresAlign) 
            Align(4);
        return new KeyValuePair<TK, TV>(key, value);
    }

    public List<KeyValuePair<TK, TV>> ReadMapWithAlign<TK, TV>(int count, Func<IReader, TK> keyConstructor,
        Func<IReader, TV> valueConstructor, bool keyRequiresAlign, bool valueRequiresAlign) where TK : notnull
    {
        var map = new List<KeyValuePair<TK, TV>>(count);
        for (int i = 0; i < count; i++)
        {
            map.Add(ReadPairWithAlign(keyConstructor, valueConstructor, keyRequiresAlign, valueRequiresAlign));
        }
        return map;
    }
}