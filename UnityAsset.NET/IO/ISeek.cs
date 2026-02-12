namespace UnityAsset.NET.IO;

public interface ISeek
{
    public long Position { get; set; }
    public long Length { get; }
    public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
            {
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                Position = offset;
                break;
            }
            case SeekOrigin.Current:
            {
                Position += offset;
                break;
            }
            case SeekOrigin.End:
            {
                if (offset > 0) throw new ArgumentOutOfRangeException(nameof(offset));
                Position = Length + offset;
                break;
            }
        }
    }

    public void Align(uint alignment)
    {
        var offset = Position % alignment;
        if (offset != 0)
            Seek(alignment - offset, SeekOrigin.Current);
    }
}