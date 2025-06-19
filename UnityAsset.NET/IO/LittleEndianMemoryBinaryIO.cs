using System.Buffers.Binary;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO;

public class LittleEndianMemoryBinaryIO : MemoryBinaryIO
{
    public override Endianness Endian => Endianness.LittleEndian;

    public LittleEndianMemoryBinaryIO(Memory<byte> data, int pos = 0) : base(data, pos) {}
    
    public LittleEndianMemoryBinaryIO(int size) : base(size) {}
    
    public override MemoryBinaryIO CastEndian(Endianness endian)
    {
        if (endian == Endian)
            return this;
        return new BigEndianMemoryBinaryIO(Data, Pos);
    }
    
    public override Int16 ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadOnlySpanBytes(2));
    public override UInt16 ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadOnlySpanBytes(2));
    public override Int32 ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadOnlySpanBytes(4));
    public override UInt32 ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadOnlySpanBytes(4));
    public override Int64 ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadOnlySpanBytes(8));
    public override UInt64 ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadOnlySpanBytes(8));
    public override float ReadFloat() => BinaryPrimitives.ReadSingleLittleEndian(ReadOnlySpanBytes(4));
    public override double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(ReadOnlySpanBytes(8));
    public override void WriteInt16(Int16 value) => BinaryPrimitives.WriteInt16LittleEndian(GetWritableSpan(2), value);
    public override void WriteUInt16(UInt16 value) => BinaryPrimitives.WriteUInt16LittleEndian(GetWritableSpan(2), value);
    public override void WriteInt32(Int32 value) => BinaryPrimitives.WriteInt32LittleEndian(GetWritableSpan(4), value);
    public override void WriteUInt32(UInt32 value) => BinaryPrimitives.WriteUInt32LittleEndian(GetWritableSpan(4), value);
    public override void WriteInt64(Int64 value) => BinaryPrimitives.WriteInt64LittleEndian(GetWritableSpan(8), value);
    public override void WriteUInt64(UInt64 value) => BinaryPrimitives.WriteUInt64LittleEndian(GetWritableSpan(8), value);
    public override void WriteFloat(float value) => BinaryPrimitives.WriteSingleLittleEndian(GetWritableSpan(4), value);
    public override void WriteDouble(double value) => BinaryPrimitives.WriteDoubleLittleEndian(GetWritableSpan(8), value);
}