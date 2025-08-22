using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class SlicedReader : IReader
{
    private readonly IReader _baseReader;
    private readonly long _offset;
    private readonly long _length;

    public long Position
    {
        get => _baseReader.Position - _offset;
        set => _baseReader.Position = _offset + value;
    }

    public void Seek(long offset) => Position = (int)offset;

    public Endianness Endian
    {
        get => _baseReader.Endian;
        set => _baseReader.Endian = value;
    }

    public long Length => _length;
    
    public byte ReadByte() => _baseReader.ReadByte();
    public byte[] ReadBytes(int count) => _baseReader.ReadBytes(count);
    
    public string ReadNullTerminatedString() => _baseReader.ReadNullTerminatedString();
    
    public SlicedReader(IReader reader, long start, long length)
    {
        _baseReader = reader;
        _offset = start;
        _length = length;
    }
}