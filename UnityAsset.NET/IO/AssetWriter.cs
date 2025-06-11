using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO;

public sealed class AssetWriter : BinaryWriter
{
    public bool BigEndian;
    
    public long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }
    
    public AssetWriter(Stream stream, bool isBigEndian = true) : base(stream)
    {
        BigEndian = isBigEndian;
    }
    
    public AssetWriter(string filePath, bool isBigEndian = true) : base(new FileStream(filePath, FileMode.Create, FileAccess.Write))
    {
        BigEndian = isBigEndian;
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
    
    public void WriteList<T>(List<T> array, Action<AssetWriter, T> writer)
    {
        foreach (var item in array)
        {
            writer(this, item);
        }
    }
    
    public void WriteListWithCount<T>(List<T> array, Action<AssetWriter, T> writer)
    {
        WriteInt32(array.Count);
        foreach (var item in array)
        {
            writer(this, item);
        }
    }
    
    public void WriteIntArrayWithCount(int[] array)
    {
        WriteInt32(array.Length);
        foreach (var item in array)
        {
            WriteInt32(item);
        }
    }
}