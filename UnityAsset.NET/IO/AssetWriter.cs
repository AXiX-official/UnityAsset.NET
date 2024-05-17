using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO;

public class AssetWriter : BinaryWriter
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
    
    public void WriteInt32(int value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteInt64(long value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteUInt16(ushort value)
    {
        Write(BigEndian ? BinaryPrimitives.ReverseEndianness(value) : value);
    }
    
    public void WriteUInt32(uint value)
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
}