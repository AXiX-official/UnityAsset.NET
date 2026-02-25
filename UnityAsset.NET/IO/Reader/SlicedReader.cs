using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class SlicedReader : IReader
{
    protected readonly IReader BaseReader;
    private readonly ulong _offset;
    public static Exception OutOfRange = new Exception("Trying to read beyond slice range.");

    # region ISeek
    public long Position
    {
        get => BaseReader.Position - (long)_offset;
        set => BaseReader.Position = (long)_offset + value;
    }
    public long Length { get; }
    
    public Endianness Endian
    {
        get => BaseReader.Endian;
        set => BaseReader.Endian = value;
    }
    
    # endregion

    public int Read(Span<byte> buffer, int offset, int count)
    {
        if (Position + count > Length)
            return BaseReader.Read(buffer, offset, (int)(Length - Position));
        return BaseReader.Read(buffer, offset, count);
    }
    
    public byte ReadByte()
    {
        if (Position + 1 > Length)
            throw OutOfRange;
        return BaseReader.ReadByte();
    }

    public byte[] ReadBytes(int count)
    {
        if (Position + count > Length)
            throw OutOfRange;
        return BaseReader.ReadBytes(count);
    }
    
    public short ReadInt16()
    {
        if (Position + 2 > Length)
            throw OutOfRange;
        return BaseReader.ReadInt16();
    }

    public ushort ReadUInt16()
    {
        if (Position + 2 > Length)
            throw OutOfRange;
        return BaseReader.ReadUInt16();
    }

    public int ReadInt32()
    {
        if (Position + 4 > Length)
            throw OutOfRange;
        return BaseReader.ReadInt32();
    }

    public uint ReadUInt32()
    {
        if (Position + 4 > Length)
            throw OutOfRange;
        return BaseReader.ReadUInt32();
    }

    public long ReadInt64()
    {
        if (Position + 8 > Length)
            throw OutOfRange;
        return BaseReader.ReadInt64();
    }

    public ulong ReadUInt64()
    {
        if (Position + 8 > Length)
            throw OutOfRange;
        return BaseReader.ReadUInt64();
    }

    public float ReadSingle()
    {
        if (Position + 4 > Length)
            throw OutOfRange;
        return BaseReader.ReadSingle();
    }

    public double ReadDouble()
    {
        if (Position + 8 > Length)
            throw OutOfRange;
        return BaseReader.ReadDouble();
    }

    public string ReadNullTerminatedString()
    {
        var str = BaseReader.ReadNullTerminatedString();
        if (Position + str.Length + 1 > Length)
            throw OutOfRange;
        return str;
    }
    
    public SlicedReader(IReaderProvider readerProvider, ulong start, ulong length, Endianness endian = Endianness.BigEndian)
    {
        BaseReader = readerProvider.CreateReader(endian);
        _offset = start;
        Length = (long)length;
        BaseReader.Position = (long)_offset;
    }
}

public class SlicedReaderProvider : IReaderProvider
{
    public readonly IReaderProvider BaseReaderProvider;
    public readonly ulong Offset;
    public readonly ulong Length;
    
    public SlicedReaderProvider(IReaderProvider readerProvider, ulong offset, ulong length)
    {
        BaseReaderProvider = readerProvider;
        Offset = offset;
        Length = length;
    }

    public IReader CreateReader(Endianness endian) => new SlicedReader(BaseReaderProvider, Offset, Length, endian);
}