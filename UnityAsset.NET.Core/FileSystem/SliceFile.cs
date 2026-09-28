using Microsoft.Win32.SafeHandles;

namespace UnityAsset.NET.FileSystem
{
    public class SliceFile : IVirtualFile, IEquatable<SliceFile>
    {
        private readonly IVirtualFile _source;
        private readonly ulong _offset;
        private long _position;

        public SliceFile(IVirtualFile source, ulong offset, ulong length)
        {
            if (offset + length > (ulong)source.Length)
                throw new ArgumentOutOfRangeException();
            _source = source.Clone();
            _offset = offset;
            Handle = _source.Handle;
            Length = (long)length;
            Position = 0;
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
                _source.Position = (long)_offset + value;
            }
        }

        public uint Read(Span<byte> buffer, uint offset, uint count)
        {
            if (offset > (uint)buffer.Length || count > (uint)buffer.Length - offset)
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"Reading {count} bytes at offset {offset} does not fit in a {buffer.Length}-byte buffer.");

            var remaining = Length - _position;
            if (remaining <= 0)
                return 0;

            var sliceCount = Math.Min(count, (uint)remaining);

            _source.Position = (long)_offset + _position;
            var read = _source.Read(buffer, offset, sliceCount);

            _position += read;
            return read;
        }

        public virtual IVirtualFile Clone()
        {
            var ret = new SliceFile(_source, _offset, (ulong)Length);
            ret.Position = Position;
            return ret;
        }

        public bool Equals(SliceFile? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            return Equals(_source, other._source)
                   && _offset == other._offset
                   && Length == other.Length;
        }

        public override bool Equals(object? obj)
            => obj is SliceFile other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                _source,
                _offset,
                Length);
        }
    }
}