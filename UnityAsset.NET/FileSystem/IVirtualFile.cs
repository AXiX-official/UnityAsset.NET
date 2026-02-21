using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem;

public interface IVirtualFile
{
    public SafeFileHandle Handle { get; }
    public long Length { get; }
    public long Position { get; set; }
    
    public uint Read(Span<byte> buffer, uint offset, uint count);

    public byte[] ReadBytes(uint count)
    {
        var buffer = new byte[count];
        Read(buffer, 0, count);
        return buffer;
    }

    public void ReadExactly(Span<byte> buffer)
    {
        var byteRead = Read(buffer, 0, (uint)buffer.Length);
        if (byteRead != buffer.Length)
            throw new Exception($"No enough bytes to read. Expect {buffer.Length} bytes but got {byteRead} bytes.");
    }
    public IVirtualFile Clone();
}