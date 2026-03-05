using System.Runtime.CompilerServices;

namespace UnityAsset.NET.Extensions;

public static class StreamExtension
{
    #if NETSTANDARD2_1
    
    public static int ReadAtLeastCore(this Stream stream, Span<byte> buffer, int minimumBytes, bool throwOnEndOfStream)
    {
        int start;
        int num;
        for (start = 0; start < minimumBytes; start += num)
        {
            num = stream.Read(buffer.Slice(start));
            if (num == 0)
            {
                if (throwOnEndOfStream)
                    throw new EndOfStreamException();
                return start;
            }
        }
        return start;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateBufferArguments(byte[] buffer, int offset, int count)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if ( (uint) count <= buffer.Length - offset)
            return;
        throw new ArgumentOutOfRangeException(nameof(count));
    }
    
    private static void ValidateReadAtLeastArguments(int bufferLength, int minimumBytes)
    {
        if (minimumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumBytes));
        if (bufferLength >= minimumBytes)
            return;
        throw new ArgumentOutOfRangeException(nameof(bufferLength));
    }
    
    public static int ReadAtLeast(this Stream stream, Span<byte> buffer, int minimumBytes, bool throwOnEndOfStream = true)
    {
        ValidateReadAtLeastArguments(buffer.Length, minimumBytes);
        return stream.ReadAtLeastCore(buffer, minimumBytes, throwOnEndOfStream);
    }
    
    public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        stream.ReadAtLeastCore(buffer.AsSpan(offset, count), count, true);
    }
    
    public static void ReadExactly(this Stream stream, Span<byte> buffer) => stream.ReadAtLeastCore(buffer, buffer.Length, true);
    
    #endif
}