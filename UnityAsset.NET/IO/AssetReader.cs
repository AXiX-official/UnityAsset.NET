﻿using System.Buffers.Binary;
using System.Text;

namespace UnityAsset.NET.IO;

public sealed class AssetReader : BinaryReader
{
    public bool BigEndian;

    private long StartPosition;
    
    public long Position
    {
        get => BaseStream.Position - StartPosition;
        set => BaseStream.Position = value + StartPosition;
    }
    
    private readonly byte[] buffer;
    
    public AssetReader(byte[] data, bool isBigEndian = true) : base(new MemoryStream(data))
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        StartPosition = 0;
    }
    
    public AssetReader(Stream stream, bool isBigEndian = true) : base(stream)
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        StartPosition = stream.Position;
    }
    
    public AssetReader(string filePath, bool isBigEndian = true) : base(new FileStream(filePath, FileMode.Open, FileAccess.Read))
    {
        buffer = new byte[8];
        BigEndian = isBigEndian;
        StartPosition = 0;
    }
    
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

    public override int ReadInt32()
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

    public override long ReadInt64()
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
    
    public override ushort ReadUInt16()
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
    
    public override uint ReadUInt32()
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

    public string ReadNullTerminated()
    {
        string output = "";
        char curChar;
        while ((curChar = ReadChar()) != 0x00)
        {
            output += curChar;
        }
        return output;
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
    
    public List<T> ReadArray<T>(int count, Func<AssetReader, T> constructor)
    {
        var array = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            array.Add(constructor(this));
        }
        return array;
    }
}