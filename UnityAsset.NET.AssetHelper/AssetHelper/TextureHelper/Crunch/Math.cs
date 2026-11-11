using System.Runtime.CompilerServices;

namespace UnityAsset.NET.AssetHelper.TextureHelper.Crunch;

public static partial class Crunch
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPowerOf2(UInt32 n) => n > 0 && (n & (n - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 NextPower2(UInt32 n)
    {
        n--;
        n |= n >> 16;
        n |= n >> 8;
        n |= n >> 4;
        n |= n >> 2;
        n |= n >> 1;
        return n + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 FloorLog2I(UInt32 n)
    {
        UInt32 l = 0;
        while (n > 1)
        {
            n >>= 1;
            l++;
        }
        return l;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 CeilLog2I(UInt32 n)
    {
        UInt32 l = FloorLog2I(n);
        if (l != IntBits && n > (1 << (int)l))
        {
            return l + 1;
        }
        return l;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt32 TotalBits(UInt32 n)
    {
        UInt32 l = 0;
        while (n > 0)
        {
            n >>= 1;
            l++;
        }
        return l;
    }
}