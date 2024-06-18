namespace UnityAsset.NET;

public static class LZ4
{
    public static unsafe int Decode(ReadOnlySpan<byte> source, Span<byte> target)
    {
        int length1 = source.Length;
        if (length1 <= 0)
            return 0;
        fixed (byte* sourcePtr = &source.GetPinnableReference())
        fixed (byte* targetPtr = &target.GetPinnableReference())
        {
            byte* s = sourcePtr;
            byte* t = targetPtr;
            byte* sourceEnd = sourcePtr + source.Length;
            while (s < sourceEnd)
            {
                byte token = *s++;
                int literalLength = token >> 4;
                int matchLength = token & 0xF;
                if (literalLength == 0xF)
                {
                    byte b;
                    do
                    {
                        b = *s++;
                        literalLength += b;
                    } while (b == 0xFF && s < sourceEnd);
                }
                Buffer.MemoryCopy(s, t, literalLength, literalLength);
                s += literalLength;
                t += literalLength;

                if (s >= sourceEnd) break;

                int offset = *s++ | (*s++ << 8);
                if (matchLength == 0xF)
                {
                    byte b;
                    do
                    {
                        b = *s++;
                        matchLength += b;
                    } while (b == 0xFF && s < sourceEnd);
                }
                matchLength += 4;
                Buffer.MemoryCopy(t - offset, t, matchLength, matchLength);
                t += matchLength;
            }
            return (int)(t - targetPtr);
        }
    }
}