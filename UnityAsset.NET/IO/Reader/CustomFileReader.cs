using System.Buffers.Binary;
using System.Text;
using UnityAsset.NET.Enums;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader;

public class CustomFileReader : IReader
{
    private readonly IVirtualFile _data; 
    
    private readonly byte[] _buffer;
    private uint _bufferOffset;
    private uint _bufferCount;
    
    public CustomFileReader(IVirtualFileInfo fileInfo, Endianness endian = Endianness.BigEndian, int bufferSize = 8192)
    {
        _data = fileInfo.GetFile();
        Endian = endian;
        
        _buffer = new byte[bufferSize];
        _bufferOffset = 0;
        _bufferCount = 0;
    }
    
    private void FillBuffer()
    {
        _bufferOffset = 0;
        _bufferCount = (uint)_data.Read(_buffer, 0, (uint)_buffer.Length);
        if (_bufferCount == 0)
            throw new EndOfStreamException();
    }
    
    # region ISeek
    public long Position
    {
        get => _data.Position - _bufferCount + _bufferOffset;
        set {
            var currentPos = Position;
            if (currentPos == value)
            {
                return;
            }
            
            if (value > currentPos && value < currentPos + (_bufferCount - _bufferOffset))
            {
                _bufferOffset += (uint)(value - currentPos);
                return;
            }
            
            _bufferOffset = 0;
            _bufferCount = 0;
            _data.Position = value;
        }
    }
    public long Length => _data.Length;
    # endregion
    
    # region IReader
    public Endianness Endian { get; set; }

    public byte ReadByte()
    {
        if (_bufferOffset >= _bufferCount)
        {
            FillBuffer();
        }
        return _buffer[_bufferOffset++];
    }
    public byte[] ReadBytes(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return [];
        if ((uint)count > ((IReader)this).Remaining)
            throw new EndOfStreamException();
        byte[] bytes = new byte[count];
        ReadExactly(bytes);
        return bytes;
    }

    public void ReadExactly(Span<byte> buffer)
    {
        int written = 0;
        while (written < buffer.Length)
        {
            if (_bufferOffset >= _bufferCount)
            {
                FillBuffer();
            }

            var available = (int)(_bufferCount - _bufferOffset);
            var toCopy = Math.Min(buffer.Length - written, available);

            _buffer.AsSpan((int)_bufferOffset, toCopy)
                .CopyTo(buffer.Slice(written, toCopy));

            _bufferOffset += (uint)toCopy;
            written += toCopy;
        }
    }
    public short ReadInt16()
    {
        if (_bufferCount - _bufferOffset >= 2)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 2);
            _bufferOffset += 2;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(span)
                : BinaryPrimitives.ReadInt16LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[2];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(tmp)
            : BinaryPrimitives.ReadInt16LittleEndian(tmp);
    }
    public ushort ReadUInt16()
    {
        if (_bufferCount - _bufferOffset >= 2)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 2);
            _bufferOffset += 2;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[2];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(tmp)
            : BinaryPrimitives.ReadUInt16LittleEndian(tmp);
    }
    public int ReadInt32()
    {
        if (_bufferCount - _bufferOffset >= 4)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 4);
            _bufferOffset += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span)
                : BinaryPrimitives.ReadInt32LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(tmp)
            : BinaryPrimitives.ReadInt32LittleEndian(tmp);
    }
    public uint ReadUInt32()
    {
        if (_bufferCount - _bufferOffset >= 4)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 4);
            _bufferOffset += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(span)
                : BinaryPrimitives.ReadUInt32LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(tmp)
            : BinaryPrimitives.ReadUInt32LittleEndian(tmp);
    }
    public long ReadInt64()
    {
        if (_bufferCount - _bufferOffset >= 8)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 8);
            _bufferOffset += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(span)
                : BinaryPrimitives.ReadInt64LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(tmp)
            : BinaryPrimitives.ReadInt64LittleEndian(tmp);
    }
    public ulong ReadUInt64()
    {
        if (_bufferCount - _bufferOffset >= 8)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 8);
            _bufferOffset += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(span)
                : BinaryPrimitives.ReadUInt64LittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadUInt64BigEndian(tmp)
            : BinaryPrimitives.ReadUInt64LittleEndian(tmp);
    }
    public float ReadSingle()
    {
        if (_bufferCount - _bufferOffset >= 4)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 4);
            _bufferOffset += 4;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadSingleBigEndian(span)
                : BinaryPrimitives.ReadSingleLittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[4];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadSingleBigEndian(tmp)
            : BinaryPrimitives.ReadSingleLittleEndian(tmp);
    }
    public double ReadDouble()
    {
        if (_bufferCount - _bufferOffset >= 8)
        {
            var span = _buffer.AsSpan((int)_bufferOffset, 8);
            _bufferOffset += 8;

            return Endian == Endianness.BigEndian
                ? BinaryPrimitives.ReadDoubleBigEndian(span)
                : BinaryPrimitives.ReadDoubleLittleEndian(span);
        }
        
        Span<byte> tmp = stackalloc byte[8];
        ReadExactly(tmp);
        return Endian == Endianness.BigEndian
            ? BinaryPrimitives.ReadDoubleBigEndian(tmp)
            : BinaryPrimitives.ReadDoubleLittleEndian(tmp);
    }
    public string ReadNullTerminatedString()
    {
        using var ms = new MemoryStream();
        while (true)
        {
            if (_bufferOffset >= _bufferCount)
            {
                FillBuffer();
            }
            byte b = _buffer[_bufferOffset++];
            if (b == 0)
                break;
            ms.WriteByte(b);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    # endregion
}

public class CustomFileReaderProvider : IReaderProvider
{
    public readonly IVirtualFileInfo FileInfo;

    public CustomFileReaderProvider(IVirtualFileInfo fileInfo)
    {
        FileInfo = fileInfo;
    }
    
    public IReader CreateReader(Endianness endian = Endianness.BigEndian) => new CustomFileReader(FileInfo, endian);
}