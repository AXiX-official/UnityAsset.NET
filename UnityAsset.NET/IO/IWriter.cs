using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

public interface IWriter : ISeek
{
    public Endianness Endian { get; }
    public IWriter CastEndian(Endianness endian);
    public Span<byte> GetWritableSpan(int count);
    public void WriteByte(byte b);
    public void WriteBytes(byte[] data);
    public void WriteBytes(Span<byte> data);
    public void WriteBoolean(Boolean value) => WriteByte(value ? (byte)1 : (byte)0);
    public void WriteInt8(sbyte value) => WriteByte((byte)value);
    public void WriteUInt8(byte value) => WriteByte(value);
    public void WriteInt16(Int16 value);
    public void WriteUInt16(UInt16 value);
    public void WriteInt32(Int32 value);
    public void WriteUInt32(UInt32 value);
    public void WriteInt64(Int64 value);
    public void WriteUInt64(UInt64 value);
    public void WriteFloat(float value);
    public void WriteDouble(double value);
    public void WriteNullTerminatedString(string value);
    public void WriteSizedString(string value);
    public void WriteList<T>(List<T> array, Action<IWriter, T> writer);
    public void WriteListWithCount<T>(List<T> array, Action<IWriter, T> writer);
    public void WriteIntArrayWithCount(int[] array);
}