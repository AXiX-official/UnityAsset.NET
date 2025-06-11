using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO;

public sealed class AssetReader : BinaryReader, ICabFile
{
    public bool BigEndian;

    private readonly long _startPosition;
    
    public long Position
    {
        get => BaseStream.Position - _startPosition;
        set => BaseStream.Position = value + _startPosition;
    }
    
    private readonly byte[] buffer;
    
    public AssetReader(byte[] data, bool isBigEndian = true, long startPosition = 0) : base(new MemoryStream(data))
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        this._startPosition = startPosition;
    }
    
    public AssetReader(Stream stream, bool isBigEndian = true, long startPosition = 0) : base(stream)
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        this._startPosition = startPosition;
    }
    
    public AssetReader(string filePath, bool isBigEndian = true, long startPosition = 0) : base(new FileStream(filePath, FileMode.Open, FileAccess.Read))
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        this._startPosition = startPosition;
    }
    
    public bool EOF => BaseStream.Position >= BaseStream.Length;
    
    public void Align(int alignment)
    {
        var pos = Position;
        var mod = pos % alignment;
        if (mod != 0)
        {
            Position += alignment - mod;
        }
    }
    
    public override Int16 ReadInt16()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 2);
            return BinaryPrimitives.ReadInt16BigEndian(buffer);
        }
        else
        {
            return base.ReadInt16();
        }
    }

    public override Int32 ReadInt32()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 4);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }
        else
        {
            return base.ReadInt32();
        }
    }

    public override Int64 ReadInt64()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 8);
            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }
        else
        {
            return base.ReadInt64();
        }
    }
    
    public override UInt16 ReadUInt16()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 2);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }
        else
        {
            return base.ReadUInt16();
        }
    }
    
    public override UInt32 ReadUInt32()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 4);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }
        else
        {
            return base.ReadUInt32();
        }
    }
    
    public override UInt64 ReadUInt64()
    {
        if (BigEndian)
        {
            Read(buffer, 0, 8);
            return BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }
        else
        {
            return base.ReadUInt64();
        }
    }
    
    public string ReadStringToNull(int maxLength = 32767)
    {
        var bytes = new List<byte>();
        int count = 0;
        while (count < maxLength)
        {
            var b = ReadByte();
            if (b == 0)
            {
                break;
            }
            bytes.Add(b);
            count++;
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public void Skip(int length)
    {
        BaseStream.Position += length;
    }
    
    public List<T> ReadList<T>(int count, Func<AssetReader, T> constructor)
    {
        var array = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            array.Add(constructor(this));
        }
        return array;
    }
    
    public byte[] ReadByteArray(int count)
    {
        byte[] array = new byte[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = ReadByte();
        }
        return array;
    }
    
    public int[] ReadIntArray(int count)
    {
        int[] array = new int[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = ReadInt32();
        }
        return array;
    }
}