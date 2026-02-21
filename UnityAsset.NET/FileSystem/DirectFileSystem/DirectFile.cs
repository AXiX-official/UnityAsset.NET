using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem.DirectFileSystem;

public class DirectFile : IVirtualFile, IEquatable<DirectFile>
{
    protected readonly long _start;
    private long _position;

    public DirectFile(SafeFileHandle handle, long start, long length)
    {
        Handle = handle;
        _start = start;
        Length = length;
    }
    
    public SafeFileHandle Handle { get; }
    
    public long Length { get; }
    
    public long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = value;
        }
    }
    
    public virtual uint Read(Span<byte> buffer, uint offset, uint count)
    {
        var toRead = Math.Min(count, Length - _position);

        int read = RandomAccess.Read(
            Handle,
            buffer.Slice((int)offset, (int)toRead),
            _start + _position);

        _position += read;
        return (uint)read;
    }

    public virtual IVirtualFile Clone()
    {
        var ret = new DirectFile(Handle, _start, Length);
        ret.Position = Position;
        return ret;
    }
    
    public bool Equals(DirectFile? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return ReferenceEquals(Handle, other.Handle)
               && _start == other._start
               && Length == other.Length;
    }

    public override bool Equals(object? obj)
        => obj is DirectFile other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Handle,
            _start,
            Length);
    }
}