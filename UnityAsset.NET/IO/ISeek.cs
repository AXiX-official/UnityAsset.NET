namespace UnityAsset.NET.IO;

public interface ISeek
{
    public long Position { get; set; }
    public void Seek(long offset);
    public void Advance(long count);
    public void Rewind(long count);
    public void Align(int alignment);
}