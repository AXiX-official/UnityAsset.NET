namespace UnityAsset.NET.AssetHelper.TextureHelper.Crunch;

public static partial class Crunch
{
    public const UInt32 HeaderMinSize = 74;
    
    private const UInt16 SigValue = (byte)'H' << 8 | (byte)'x';
    private const UInt32 MaxExpectedCodeSize = 16;
    private const UInt32 MaxSupportedSyms = 8192;
    private const UInt32 MaxTableBits = 11;
    private const UInt32 BitBufSize = 32;
    
    private const UInt32 MaxCodeLengthCodes = 21;
    
    private const UInt32 SmallZeroRunCode = 17;
    private const UInt32 LargeZeroRunCode = 18;
    private const UInt32 SmallRepeatCode = 19;
    private const UInt32 LargeRepeatCode = 20;
    private const UInt32 MinSmallZeroRunSize = 3;
    private const UInt32 MinLargeZeroRunSize = 11;
    private const UInt32 SmallMinNonZeroRunSize = 3;
    private const UInt32 LargeMinNonZeroRunSize = 7;
    private const UInt32 SmallZeroRunExtraBits = 3;
    private const UInt32 LargeZeroRunExtraBits = 7;
    private const UInt32 SmallNonZeroRunExtraBits = 2;
    private const UInt32 LargeNonZeroRunExtraBits = 6;
    
    private static readonly byte[] MostProbableCodeLengthCodes = [
        (byte)SmallZeroRunCode,
        (byte)LargeZeroRunCode,
        (byte)SmallRepeatCode,
        (byte)LargeRepeatCode,
        0,
        8,
        7,
        9,
        6,
        10,
        5,
        11,
        4,
        12,
        3,
        13,
        2,
        14,
        1,
        15,
        16,
    ];
    
    private static readonly byte[] Dxt5FromLinear = [0, 2, 3, 4, 5, 6, 7, 1];
    
    private const UInt32 IntBits = 32;
    private enum CrnFmt : UInt32
    {
        DXT1,
        FirstValid = 0,
        DXT3,
        DXT5,
        
        // Various DXT5 derivatives
        DXT5_CCxY,  // Luma-chroma
        DXT5_xGxR,  // Swizzled 2-component
        DXT5_xGBR,  // Swizzled 3-component
        DXT5_AGBR,  // Swizzled 4-component
        
        // ATI 3DC and X360 DXN
        DXN_XY,
        DXN_YX,
        
        // DXT5 alpha blocks only
        DXT5A,
        
        ETC1,
        ETC2,
        ETC2A,
        ETC1S,
        ETC2AS,
        
        Total,
        ForceDWORD = 0xFFFFFFFF
    }
}