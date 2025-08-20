using System.Text;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.IO.Reader;

public class CustomStreamReader : IReader, IDisposable
{
    private readonly System.IO.Stream _stream; 
    private readonly bool _leaveOpen;

    public CustomStreamReader(System.IO.Stream stream, Endianness endian = Endianness.BigEndian, bool leaveOpen = false)
    {
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Stream must be readable and seekable", nameof(stream));
        _stream = stream;
        Endian = endian;
        _leaveOpen = leaveOpen;
    }
    
    # region ISeek
    public long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }
    public void Seek(long offset) => Position = offset;
    # endregion
    
    # region IReader
    public Endianness Endian { get; set; }
    public long Length => _stream.Length;
    public byte ReadByte() =>(byte)_stream.ReadByte();
    public byte[] ReadBytes(int count)
    {
        if (count == 0)
            return [];
        byte[] bytes = new byte[count];
        _stream.ReadExactly(bytes.AsSpan(0, count));
        return bytes;
    }
    public string ReadNullTerminatedString()
    {
        using var ms = new MemoryStream();
        byte b;
        while ((b = ReadByte()) != 0)
            ms.WriteByte(b);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    # endregion
    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}