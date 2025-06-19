using System.Buffers.Binary;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

public class BigEndianMemoryBinaryIO : MemoryBinaryIO
{
    public override Endianness Endian => Endianness.BigEndian;

    public BigEndianMemoryBinaryIO(Memory<byte> data, int pos = 0) : base(data, pos) {}
    
    public BigEndianMemoryBinaryIO(int size) : base(size) {}
    
    public override MemoryBinaryIO CastEndian(Endianness endian)
    {
        if (endian == Endian)
            return this;
        return new LittleEndianMemoryBinaryIO(Data, Pos);
    }

    public override Int16 ReadInt16() => BinaryPrimitives.ReadInt16BigEndian(ReadOnlySpanBytes(2));
    public override UInt16 ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadOnlySpanBytes(2));
    public override Int32 ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(ReadOnlySpanBytes(4));
    public override UInt32 ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadOnlySpanBytes(4));
    public override Int64 ReadInt64() => BinaryPrimitives.ReadInt64BigEndian(ReadOnlySpanBytes(8));
    public override UInt64 ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(ReadOnlySpanBytes(8));
    public override float ReadFloat() => BinaryPrimitives.ReadSingleBigEndian(ReadOnlySpanBytes(4));
    public override double ReadDouble() => BinaryPrimitives.ReadDoubleBigEndian(ReadOnlySpanBytes(8));
    public override void WriteInt16(Int16 value) => BinaryPrimitives.WriteInt16BigEndian(GetWritableSpan(2), value);
    public override void WriteUInt16(UInt16 value) => BinaryPrimitives.WriteUInt16BigEndian(GetWritableSpan(2), value);
    public override void WriteInt32(Int32 value) => BinaryPrimitives.WriteInt32BigEndian(GetWritableSpan(4), value);
    public override void WriteUInt32(UInt32 value) => BinaryPrimitives.WriteUInt32BigEndian(GetWritableSpan(4), value);
    public override void WriteInt64(Int64 value) => BinaryPrimitives.WriteInt64BigEndian(GetWritableSpan(8), value);
    public override void WriteUInt64(UInt64 value) => BinaryPrimitives.WriteUInt64BigEndian(GetWritableSpan(8), value);
    public override void WriteFloat(float value) => BinaryPrimitives.WriteSingleBigEndian(GetWritableSpan(4), value);
    public override void WriteDouble(double value) => BinaryPrimitives.WriteDoubleBigEndian(GetWritableSpan(8), value);
}