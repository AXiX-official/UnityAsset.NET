using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class SlicedReader : IReader
{
    protected readonly IReader BaseReader;
    private readonly ulong _offset;

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
    
    void IReader.Align(uint alignment)
    {
        BaseReader.Align(alignment);
    }
    # endregion
    
    
    public byte ReadByte()
    {
        if (Position + 1 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadByte();
    }

    public byte[] ReadBytes(int count)
    {
        if (Position + count > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadBytes(count);
    }

    public short ReadInt16()
    {
        if (Position + 2 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadInt16();
    }

    public ushort ReadUInt16()
    {
        if (Position + 2 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadUInt16();
    }

    public int ReadInt32()
    {
        if (Position + 4 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadInt32();
    }

    public uint ReadUInt32()
    {
        if (Position + 4 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadUInt32();
    }

    public long ReadInt64()
    {
        if (Position + 8 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadInt64();
    }

    public ulong ReadUInt64()
    {
        if (Position + 8 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadUInt64();
    }

    public float ReadSingle()
    {
        if (Position + 4 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadSingle();
    }

    public double ReadDouble()
    {
        if (Position + 8 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return BaseReader.ReadDouble();
    }

    public string ReadNullTerminatedString()
    {
        var str = BaseReader.ReadNullTerminatedString();
        if (Position + str.Length + 1 > Length)
            throw new ArgumentOutOfRangeException(nameof(_offset));
        return str;
    }
    
    public SlicedReader(IReaderProvider readerProvider, ulong start, ulong length, Endianness endian = Endianness.BigEndian)
    {
        BaseReader = readerProvider.CreateReader(endian);
        _offset = start;
        Length = (long)length;
        BaseReader.Position = (long)_offset;
    }

    public void Dispose()
    {
        BaseReader.Dispose();
    }
}

public class SlicedReaderProvider : IReaderProvider
{
    protected readonly IReaderProvider BaseReaderProvider;
    protected readonly ulong Start;
    protected readonly ulong Length;
    
    public SlicedReaderProvider(IReaderProvider readerProvider, ulong start, ulong length)
    {
        BaseReaderProvider = readerProvider;
        Start = start;
        Length = length;
    }

    public IReader CreateReader(Endianness endian) => new SlicedReader(BaseReaderProvider, Start, Length, endian);
}