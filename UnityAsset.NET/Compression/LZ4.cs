namespace UnityAsset.NET;

public static unsafe class LZ4
{
    // Only about half the speed of K4os.Compression.LZ4
    
    public static int Decode(ReadOnlySpan<byte> source, Span<byte> target)
    {
        int length = source.Length;
        if (length < 5)
            return 0;
        fixed (byte* sourcePtr = &source.GetPinnableReference())
        fixed (byte* targetPtr = &target.GetPinnableReference())
        {
            byte* s = sourcePtr;
            byte* t = targetPtr;
            byte* sourceEnd = sourcePtr + length;
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
                if (s == sourceEnd && matchLength == 0) break;
                if (s >= sourceEnd) return -1;
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
                while (matchLength > offset)
                {
                    Buffer.MemoryCopy(t - offset, t, offset, offset);
                    t += offset;
                    matchLength -= offset;
                }
                if (matchLength > 0)
                {
                    Buffer.MemoryCopy(t - offset, t, matchLength, matchLength);
                }
                t += matchLength;
            }
            return (int)(t - targetPtr);
        }
    }
    
    public static int EncodeFast(ReadOnlySpan<byte> source, Span<byte> target)
    {
        int sourceLength = source.Length - 5;
        if (sourceLength < 0)
        {
            return 0;
        }
        fixed (byte* sourcePtr = &source.GetPinnableReference())
        fixed (byte* targetPtr = &target.GetPinnableReference())
        {
            byte* anchor = sourcePtr;
            IntPtr[] hashTable = new IntPtr[1 << 16];
            
            byte* s = sourcePtr;
            byte* t = targetPtr;
            byte* sourceEnd = sourcePtr + sourceLength;
            while (s < sourceEnd - 4)
            {
                uint data =  *(uint*) s;
                ushort hash =(ushort) ((data * 2654435761) >> 16);
                if (hashTable[hash] == 0)
                {
                    hashTable[hash] = (IntPtr)s;
                    s++;
                }
                else
                {
                    byte* sourceOffset = (byte*)hashTable[hash];
                    uint oldData =  *(uint*) sourceOffset;
                    if (oldData != data)
                    {
                        hashTable[hash] = (IntPtr)s;
                        s++;
                    }
                    else
                    {
                        hashTable[hash] = (IntPtr)s;
                        int matchDec = (int) (s - sourceOffset);
                        if (matchDec > 0xFFFF)
                        {
                            s++;
                            continue;
                        }
                        int literalLen = (int) (s - anchor);
                        int matchLen = 4;
                        while (s + matchLen < sourceEnd && *(s + matchLen) == *(sourceOffset + matchLen))
                        {
                            matchLen++;
                        }
                        int token = Math.Min(literalLen, 15) << 4 | Math.Min(matchLen - 4, 15);
                        *t++ = (byte)token;
                        if (literalLen >= 15)
                        {
                            int l = literalLen - 15;
                            while (l >= 0xFF)
                            {
                                *t++ = 0xFF;
                                l -= 0xFF;
                            }
                            *t++ = (byte)l;
                        }
                        Buffer.MemoryCopy(anchor, t, literalLen, literalLen);
                        t += literalLen;
                        byte matchDecLow = (byte)matchDec;
                        byte matchDecHigh = (byte)(matchDec >> 8);
                        *t++ = matchDecLow;
                        *t++ = matchDecHigh;
                        if (matchLen >= 19)
                        {
                            int l = matchLen - 19;
                            while (l >= 0xFF)
                            {
                                *t++ = 0xFF;
                                l -= 0xFF;
                            }
                            *t++ = (byte)l;
                        }
                        s += matchLen;
                        anchor = s;
                    }
                }
            }
            int literalLenFinal = (int) (sourceEnd - anchor + 5);
            int tokenFinal = Math.Min(literalLenFinal, 15) << 4;
            *t++ = (byte)tokenFinal;
            if (literalLenFinal >= 15)
            {
                int l = literalLenFinal - 15;
                while (l >= 255)
                {
                    *t++ = 255;
                    l -= 255;
                }
                *t++ = (byte)l;
            }
            Buffer.MemoryCopy(anchor, t, literalLenFinal, literalLenFinal);
            t += literalLenFinal;
            return (int)(t - targetPtr);
        }
    }
    
    public static int MaximumOutputSize(int inputSize)
    {
        return inputSize + inputSize / 255 + 16;
    }
}