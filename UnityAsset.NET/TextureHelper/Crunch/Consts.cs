using System.Runtime.InteropServices;

namespace UnityAsset.NET.TextureHelper.Crunch;

// based on https://github.com/UniversalGameExtraction/texture2ddecoder/blob/master/src/crunch/crn_consts.rs
public static partial class Crunch
{
    public const UInt16 CRNSIG_VALUE = (byte)'H' << 8 | (byte)'x';
    public const UInt32 MAX_EXPECTED_CODE_SIZE = 16;
    public const UInt32 MAX_SUPPORTED_SYMS = 8192;
    public const UInt32 MAX_TABLE_BITS = 11;
    public const UInt32 MAGIC_VALUE = 0x1EF9CABD;
    public const UInt32 BIT_BUF_SIZE = 32;
    public const UInt32 CRNMAX_LEVELS = 16;
    
    // The crnd library assumes all allocation blocks have at least CRND_MIN_ALLOC_ALIGNMENT alignment.
    // public const UInt32 CRND_MIN_ALLOC_ALIGNMENT = 8;

    // Code length encoding symbols:
    // 0-16 - actual code lengths
    public const UInt32 MAX_CODELENGTH_CODES = 21;
    
    public const UInt32 SMALL_ZERO_RUN_CODE = 17;
    public const UInt32 LARGE_ZERO_RUN_CODE = 18;
    public const UInt32 SMALL_REPEAT_CODE = 19;
    public const UInt32 LARGE_REPEAT_CODE = 20;

    public const UInt32 MIN_SMALL_ZERO_RUN_SIZE = 3;
    // public const UInt32 cMaxSmallZeroRunSize = 10;
    public const UInt32 MIN_LARGE_ZERO_RUN_SIZE = 11;
    // public const UInt32 cMaxLargeZeroRunSize = 138;

    public const UInt32 SMALL_MIN_NON_ZERO_RUN_SIZE = 3;
    // public const UInt32 cSmallMaxNonZeroRunSize = 6;
    public const UInt32 LARGE_MIN_NON_ZERO_RUN_SIZE = 7;
    // public const UInt32 cLargeMaxNonZeroRunSize = 70;

    public const UInt32 SMALL_ZERO_RUN_EXTRA_BITS = 3;
    public const UInt32 LARGE_ZERO_RUN_EXTRA_BITS = 7;
    public const UInt32 SMALL_NON_ZERO_RUN_EXTRA_BITS = 2;
    public const UInt32 LARGE_NON_ZERO_RUN_EXTRA_BITS = 6;
    
    public static readonly byte[] MOST_PROBABLE_CODELENGTH_CODES = [
        (byte)SMALL_ZERO_RUN_CODE,
        (byte)LARGE_ZERO_RUN_CODE,
        (byte)SMALL_REPEAT_CODE,
        (byte)LARGE_REPEAT_CODE,
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

    // public const UInt32 cNumMostProbableCodelengthCodes = 21;
    
    public static readonly bool CRND_LITTLE_ENDIAN_PLATFORM = BitConverter.IsLittleEndian;
    
    // public const UInt32 cDXTBlockShift = 2;
    // public const UInt32 cDXTBlockSize = 1 << cDXTBlockShift;
    // public const UInt32 cDXT1BytesPerBlock = 8;
    // public const UInt32 cDXT5NBytesPerBlock = 16;
    public const UInt32 DXT1_SELECTOR_BITS = 2;
    public const UInt32 DXT1_SELECTOR_VALUES = 1 << (int)DXT1_SELECTOR_BITS;
    // public const UInt32 cDXT1SelectorMask = cDXT1SelectorValues - 1;
    public const UInt32 DXT5_SELECTOR_BITS = 3;
    public const UInt32 DXT5_SELECTOR_VALUES = 1 << (int)DXT5_SELECTOR_BITS;
    // public const UInt32 cDXT5SelectorMask = cDXT5SelectorValues - 1;
    
    // public static readonly byte[] g_dxt1_to_linear = [0, 3, 1, 2];
    public static readonly byte[] DXT1_FROM_LINEAR = [0, 2, 3, 1];
    // public static readonly byte[] g_dxt5_to_linear = [0, 7, 1, 2, 3, 4, 5, 6];
    public static readonly byte[] DXT5_FROM_LINEAR = [0, 2, 3, 4, 5, 6, 7, 1];
    // public static readonly byte[] g_six_alpha_invert_table = [1, 0, 5, 4, 3, 2, 6, 7];
    // public static readonly byte[] g_eight_alpha_invert_table = [1, 0, 7, 6, 5, 4, 3, 2];
    
    public const UInt32 NUM_CHUNK_ENCODINGS = 8;
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct crnd_encoding_tile_indices
    {
        public fixed byte tiles[4];
        
        public crnd_encoding_tile_indices(byte a, byte b, byte c, byte d)
        {
            tiles[0] = a;
            tiles[1] = b;
            tiles[2] = c;
            tiles[3] = d;
        }
    
        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= 4)
                    throw new IndexOutOfRangeException();
                return tiles[index];
            }
            set
            {
                if (index < 0 || index >= 4)
                    throw new IndexOutOfRangeException();
                tiles[index] = value;
            }
        }
    }
    
    public static readonly crnd_encoding_tile_indices[] CRND_CHUNK_ENCODING_TILES = [
        new (0, 0, 0, 0),
        new (0, 0, 1, 1),
        new (0, 1, 0, 1),
        new (0, 0, 1, 2),
        new (1, 2, 0, 0),
        new (0, 1, 0, 2),
        new (1, 0, 2, 0),
        new (0, 1, 2, 3)
    ];
    
    public static readonly byte[] CRND_CHUNK_ENCODING_NUM_TILES = [1, 2, 2, 3, 3, 3, 3, 4];
    
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