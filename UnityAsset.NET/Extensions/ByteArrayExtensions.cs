namespace UnityAsset.NET.Extensions;

public static class ByteArrayExtensions
{
    public static byte[] ToUInt4Array(this byte[] source) => ToUInt4Array(source, 0, source.Length);
    public static byte[] ToUInt4Array(this byte[] source, int offset, int size)
    {
        var buffer = new byte[size * 2];
        for (var i = 0; i < size; i++)
        {
            var idx = i * 2;
            buffer[idx] = (byte)(source[offset + i] >> 4);
            buffer[idx + 1] = (byte)(source[offset + i] & 0xF);
        }
        return buffer;
    }
}