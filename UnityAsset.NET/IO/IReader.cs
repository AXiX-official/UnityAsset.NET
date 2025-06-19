using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;

namespace UnityAsset.NET.IO;

public interface IReader : ISeek, IFile
{
    public Endianness Endian { get; }
    public IReader CastEndian(Endianness endian);
    public long Length { get; }
    public ReadOnlySpan<byte> ReadOnlySlice(long start, int length);
    public byte ReadByte();
    public byte[] ReadBytes(int count);
    public ReadOnlySpan<byte> ReadOnlySpanBytes(int count);
    public Boolean ReadBoolean() => ReadByte() != 0;
    public sbyte ReadInt8() => (sbyte)ReadByte();
    public byte ReadUInt8() => ReadByte();
    public Int16 ReadInt16();
    public UInt16 ReadUInt16();
    public Int32 ReadInt32();
    public UInt32 ReadUInt32();
    public Int64 ReadInt64();
    public UInt64 ReadUInt64();
    public float ReadFloat();
    public double ReadDouble();
    public string ReadNullTerminatedString();
    public string ReadSizedString();
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
}