using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Writer;

public class CustomStreamWriter : IWriter, IDisposable
{
    private readonly System.IO.Stream _stream; 
    private readonly bool _leaveOpen;
    
    private readonly byte[] _buffer;
    private uint _bufferCount;
    
    public CustomStreamWriter(System.IO.Stream stream, Endianness endian = Endianness.BigEndian, bool leaveOpen = false, int bufferSize = 8192)
    {
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("Stream must be writable and seekable", nameof(stream));
        _stream = stream;
        Endian = endian;
        _leaveOpen = leaveOpen;
        
        _buffer = new byte[bufferSize];
        _bufferCount = 0;
    }

    private void FlushBuffer()
    {
        _stream.Write(_buffer, 0, (int)_bufferCount);
        _bufferCount = 0;
    }
    
    # region ISeek
    public long Position
    {
        get => _stream.Position + _bufferCount;
        set {
            var streamPos = _stream.Position;
            var currentPos = streamPos + _bufferCount;
            
            if (currentPos == value)
            {
                return;
            }

            if (value >= streamPos && value <= streamPos + _buffer.Length)
            {
                var newCount = (uint)(value - streamPos);
                if (newCount > _bufferCount)
                {
                    _buffer.AsSpan((int)_bufferCount, (int)(newCount - _bufferCount)).Clear();
                }
                _bufferCount = newCount;
                return;
            }

            FlushBuffer();
            _stream.Seek(value, SeekOrigin.Begin);
        }
    }
    public long Length => _stream.Length;
    # endregion

    # region IWriter

    public Endianness Endian { get; set; }
    public void WriteByte(byte value)
    {
        if (_bufferCount == _buffer.Length)
            FlushBuffer();
        _buffer[_bufferCount++] = value;
    }
    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= _buffer.Length)
        {
            FlushBuffer();
            _stream.Write(bytes);
            return;
        }
        
        int offset = 0;
        int remaining = bytes.Length;
        while (remaining > 0)
        {
            int space = _buffer.Length - (int)_bufferCount;
            if (space == 0)
            {
                FlushBuffer();
                continue;
            }

            int toCopy = Math.Min(remaining, space);
            bytes.Slice(offset, toCopy)
                .CopyTo(_buffer.AsSpan((int)_bufferCount, toCopy));

            offset += toCopy;
            remaining -= toCopy;
            _bufferCount += (uint)toCopy;
        }
    }
    # endregion
    
    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}