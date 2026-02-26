using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Writer;

public class CustomStreamWriter : IWriter, IDisposable
{
    private readonly Stream _stream; 
    private readonly bool _leaveOpen;
    
    private readonly byte[] _buffer;
    private int _bufferPos;
    private int BuffRemaining => _buffer.Length - _bufferPos;
    
    public CustomStreamWriter(Stream stream, Endianness endian = Endianness.BigEndian, bool leaveOpen = false, int bufferSize = 8192)
    {
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("Stream must be writable and seekable", nameof(stream));
        _stream = stream;
        Endian = endian;
        _leaveOpen = leaveOpen;
        
        _buffer = new byte[bufferSize];
    }

    private void FlushBuffer()
    {
        _stream.Write(_buffer, 0, _bufferPos);
        _bufferPos = 0;
    }
    
    # region ISeek
    public long Position
    {
        get => _stream.Position + _bufferPos;
        set {
            var streamPos = _stream.Position;
            var currentPos = streamPos + _bufferPos;
            
            if (currentPos == value)
            {
                return;
            }

            if (value >= streamPos && value <= streamPos + _buffer.Length)
            {
                var newCount = (int)(value - streamPos);
                if (newCount > _bufferPos)
                {
                    _buffer.AsSpan(_bufferPos, newCount - _bufferPos).Clear();
                }
                _bufferPos = newCount;
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
        if (BuffRemaining == 0)
            FlushBuffer();
        _buffer[_bufferPos++] = value;
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
            int toCopy = Math.Min(remaining, BuffRemaining);
            bytes.Slice(offset, toCopy)
                .CopyTo(_buffer.AsSpan(_bufferPos, toCopy));

            offset += toCopy;
            remaining -= toCopy;
            _bufferPos += toCopy;
            
            if (BuffRemaining == 0)
            {
                FlushBuffer();
            }
        }
    }
    public ulong WriteBytes(IReader reader)
    {
        ulong totalBytesRead = 0;
        while (reader.Remaining > 0)
        {
            var bytesRead = reader.Read(_buffer, _bufferPos, BuffRemaining);
            _bufferPos += bytesRead;
            
            if (BuffRemaining == 0)
            {
                FlushBuffer();
            }

            totalBytesRead += (uint)bytesRead;
        }

        return totalBytesRead;
    }
    # endregion

    public void WriteStream(Stream stream)
    {
        FlushBuffer();
        stream.CopyTo(_stream);
    }

    public void Finish()
    {
        FlushBuffer();
    }
    
    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}