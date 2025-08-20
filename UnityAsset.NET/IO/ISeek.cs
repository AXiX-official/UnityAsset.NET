namespace UnityAsset.NET.IO;

public interface ISeek
{
    public long Position { get; set; }
    public void Seek(long offset);
    public void Advance(long count) => Seek(Position + count);
    public void Rewind(long count) => Seek(Position - count);
    public void Align(int alignment)
    {
        var offset = Position % alignment;
        if (offset != 0)
            Advance(alignment - offset);
    }
}