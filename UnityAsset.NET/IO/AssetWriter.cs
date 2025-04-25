using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO;

public sealed class AssetWriter : BinaryWriter
{
    public bool BigEndian;
    
    public AssetWriter(Stream stream, bool isBigEndian = true) : base(stream)
    {
        BigEndian = isBigEndian;
    }
    
    public void AlignStream(int alignment)
    {
        int offset = (int)BaseStream.Position % alignment;
        if (offset != 0)
        {
            Write(new byte[alignment - offset]);
        }
    }

    public void Align(int alignment)
    {
        var mod = BaseStream.Position % alignment;
        if (mod != 0)
        {
            BaseStream.Position += alignment - mod;
        }
    }
    
    public void WriteBoolean(bool value)
    {
        Write(value ? (byte)1 : (byte)0);
    }
    
    public void WriteInt16(Int16 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteInt32(Int32 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteInt64(Int64 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteUInt16(UInt16 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteUInt32(UInt32 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteUInt64(UInt64 value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteStringToNull(string str)
    {
        Write(Encoding.UTF8.GetBytes(str));
        Write((byte)0);
    }
    
    public void WriteStream(Stream stream)
    {
        stream.CopyTo(BaseStream);
    }
    
    public void WriteArray<T>(List<T> array, Action<AssetWriter, T> writer)
    {
        foreach (var item in array)
        {
            writer(this, item);
        }
    }
}