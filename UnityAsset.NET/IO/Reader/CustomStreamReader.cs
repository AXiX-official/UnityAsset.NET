using System.Text;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class CustomStreamReader : IReader, IDisposable
{
    private readonly System.IO.Stream _stream; 
    private readonly bool _leaveOpen;
    
    private readonly byte[] _buffer;
    private int _bufferOffset;
    private int _bufferCount;

    public CustomStreamReader(System.IO.Stream stream, Endianness endian = Endianness.BigEndian, bool leaveOpen = false, int bufferSize = 8192)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Stream must be readable and seekable", nameof(stream));
        _stream = stream;
        Endian = endian;
        _leaveOpen = leaveOpen;
        
        _buffer = new byte[bufferSize];
        _bufferOffset = 0;
        _bufferCount = 0;
    }
    
    private void FillBuffer()
    {
        _bufferOffset = 0;
        _bufferCount = _stream.Read(_buffer, 0, _buffer.Length);
    }
    
    # region ISeek
    public long Position
    {
        get => _stream.Position - _bufferCount + _bufferOffset;
        set => Seek(value);
    }

    public void Seek(long offset)
    {
        var currentPos = Position;
        if (currentPos == offset)
        {
            return;
        }

        if (offset > currentPos && offset < currentPos + (_bufferCount - _bufferOffset))
        {
            _bufferOffset += (int)(offset - currentPos);
            return;
        }
        
        _bufferOffset = 0;
        _bufferCount = 0;
        _stream.Seek(offset, SeekOrigin.Begin);
    }
    # endregion
    
    # region IReader
    public Endianness Endian { get; set; }
    public long Length => _stream.Length;

    public byte ReadByte()
    {
        if (_bufferOffset >= _bufferCount)
        {
            FillBuffer();
            if (_bufferCount == 0)
                throw new EndOfStreamException();
        }
        return _buffer[_bufferOffset++];
    }
    public byte[] ReadBytes(int count)
    {
        if (count == 0)
            return [];
        if (Position + count > _stream.Length)
            throw new EndOfStreamException();
        byte[] bytes = new byte[count];
        int bytesRead = 0;
        while (bytesRead < count)
        {
            int bytesAvailable = _bufferCount - _bufferOffset;
            if (bytesAvailable == 0)
            {
                FillBuffer();
                bytesAvailable = _bufferCount;
                if (bytesAvailable == 0)
                    break;
            }
            
            int bytesToCopy = Math.Min(count - bytesRead, bytesAvailable);
            Buffer.BlockCopy(_buffer, _bufferOffset, bytes, bytesRead, bytesToCopy);
            
            _bufferOffset += bytesToCopy;
            bytesRead += bytesToCopy;
        }

        if (bytesRead < count)
            throw new EndOfStreamException();
        
        return bytes;
    }
    public string ReadNullTerminatedString()
    {
        using var ms = new MemoryStream();
        while (true)
        {
            if (_bufferOffset >= _bufferCount)
            {
                FillBuffer();
                if (_bufferCount == 0)
                    throw new EndOfStreamException();
            }
            byte b = _buffer[_bufferOffset++];
            if (b == 0)
                break;
            ms.WriteByte(b);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    # endregion
    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}