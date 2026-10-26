using System.Text;

namespace UnityAsset.NET.IO.Writer;

// for measuring how many bytes a write would produce without actually writing anything
public sealed class CountWriter : IWriter
{
    private long _position;
    private long _length;

    public CountWriter(Endianness endian = Endianness.BigEndian)
    {
        Endian = endian;
    }

    # region ISeek

    public long Position
    {
        get => _position;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Position cannot be negative.");

            _position = value;
            
            if (value > _length)
                _length = value;
        }
    }

    public long Length => _length;

    public void Align(uint alignment)
    {
        if (alignment == 0)
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be greater than 0.");

        var offset = _position % alignment;
        if (offset != 0)
            Advance(alignment - offset);
    }

    # endregion

    # region IWriter

    public Endianness Endian { get; set; }

    public void WriteByte(byte value) => Advance(1);
    public void WriteSByte(sbyte value) => Advance(1);
    public void WriteInt8(sbyte value) => Advance(1);
    public void WriteUInt8(byte value) => Advance(1);
    public void WriteBoolean(bool value) => Advance(1);
    public void WriteChar(char value) => Advance(2);
    public void WriteInt16(short value) => Advance(2);
    public void WriteUInt16(ushort value) => Advance(2);
    public void WriteInt32(int value) => Advance(4);
    public void WriteUInt32(uint value) => Advance(4);
    public void WriteInt64(long value) => Advance(8);
    public void WriteUInt64(ulong value) => Advance(8);
    public void WriteSingle(float value) => Advance(4);
    public void WriteDouble(double value) => Advance(8);

    public void WriteBytes(ReadOnlySpan<byte> bytes) => Advance(bytes.Length);
    public void WriteBytes(byte value, ulong count) => Advance(checked((long)count));
    public void WriteNullTerminatedString(string value) => Advance(Encoding.UTF8.GetByteCount(value) + 1);

    public void WriteSizedString(string value)
    {
        Advance(4 + Encoding.UTF8.GetByteCount(value));
        Align(4);
    }

    public ulong WriteBytes(IReader reader)
    {
        if (reader == null)
            throw new ArgumentNullException(nameof(reader));

        var remaining = reader.Remaining;
        if (remaining <= 0)
            return 0;
        
        reader.Position += remaining;
        Advance(remaining);
        return (ulong)remaining;
    }

    public void WriteStream(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining > 0)
            {
                stream.Seek(0, SeekOrigin.End);
                Advance(remaining);
            }

            return;
        }
        
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            Advance(read);
    }

    # endregion
    
    public static long Measure(Action<IWriter> write, Endianness endian = Endianness.BigEndian)
    {
        if (write == null)
            throw new ArgumentNullException(nameof(write));

        var writer = new CountWriter(endian);
        write(writer);
        return writer.Length;
    }

    private void Advance(long count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Cannot advance by a negative number of bytes.");

        _position = checked(_position + count);
        if (_position > _length)
            _length = _position;
    }
}