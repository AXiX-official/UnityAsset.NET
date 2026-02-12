using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public interface IReader : ISeek, IFile
{
    # region ISeek

    public new void Align(uint alignment)
    {
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
    
    public byte ReadByte();
    public sbyte ReadSByte() => (sbyte)ReadByte();
    public byte[] ReadBytes(int count);
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
    public string ReadSizedString()
    {
        var length = ReadInt32();
        if (length > (int)Remaining || length < 0)
            // TODO:
            return String.Empty;
        var ret = length > 0 ? Encoding.UTF8.GetString(ReadBytes(length)) : String.Empty;
        Align(4);
        return ret;
    }
    public List<T> ReadList<T>(int count, Func<IReader, T> constructor)
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(constructor(this));
        return list;
    }
    public List<T> ReadList<T>(Func<IReader, T> constructor) => ReadList(ReadInt32(), constructor);
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
    public List<T> ReadListWithAlign<T>(Func<IReader, T> constructor, bool requiresAlign) =>
        ReadListWithAlign(ReadInt32(), constructor, requiresAlign);
    public T[] ReadArray<T>(int count, Func<IReader, T> constructor)
    {
        var array = new T[count];
        for (int i = 0; i < count; i++)
            array[i] = constructor(this);
        return array;
    }
    public T[] ReadArray<T>(Func<IReader, T> constructor) => ReadArray(ReadInt32(), constructor);
    public T[] ReadArrayWithAlign<T>(int count, Func<IReader, T> constructor, bool requiresAlign)
    {
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
        return new (key, value);
    }
    public void ReadFixedArray<T>(in T[] array, Func<IReader, T> constructor) where T : struct
    {
        for (int i = 0; i < array.Length; i++)
            array[i] = constructor(this);
    }
}