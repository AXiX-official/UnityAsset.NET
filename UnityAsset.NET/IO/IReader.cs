using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public interface IReader : ISeek, IFile
{
    public Endianness Endian { get; set; }
    public long Length { get; }
    public byte ReadByte();
    public byte[] ReadBytes(int count);
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
        return length > 0 ? Encoding.UTF8.GetString(ReadBytes(length)) : "";
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

    public List<KeyValuePair<K, V>> ReadMapWithAlign<K, V>(int count, Func<IReader, K> keyConstructor,
        Func<IReader, V> valueConstructor, bool keyRequiresAlign, bool valueRequiresAlign) where K : notnull
    {
        var map = new List<KeyValuePair<K, V>>(count);
        for (int i = 0; i < count; i++)
        {
            K key = keyConstructor(this);
            if (keyRequiresAlign) 
                Align(4);
            V value = valueConstructor(this);
            if (valueRequiresAlign) 
                Align(4);
            map.Add(new KeyValuePair<K, V>(key, value));
        }
        return map;
    }
}