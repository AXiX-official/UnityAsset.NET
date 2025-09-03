namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    public const UInt32 HeaderMinSize = 74;
    
    public class Header {
        // crn_packed_uint<2>    m_sig;
        // crn_packed_uint<2>    m_header_size;
        // crn_packed_uint<2>    m_header_crc16;
        public PackedUint Sig;
        public PackedUint HeaderSize;
        public PackedUint HeaderCrc16;
        
        // crn_packed_uint<4>    m_data_size;
        // crn_packed_uint<2>    m_data_crc16;
        public PackedUint DataSize;
        public PackedUint DataCrc16;

        // crn_packed_uint<2>    m_width;
        // crn_packed_uint<2>    m_height;
        public PackedUint Width;
        public PackedUint Height;
        
        // crn_packed_uint<1>    m_levels;
        // crn_packed_uint<1>    m_faces;
        public PackedUint Levels;
        public PackedUint Faces;
        
        // crn_packed_uint<1>    m_format;
        // crn_packed_uint<2>    m_flags;
        public PackedUint Format;
        public PackedUint Flags;
        
        // crn_packed_uint<4>    m_reserved;
        // crn_packed_uint<4>    m_userdata0;
        // crn_packed_uint<4>    m_userdata1;
        public PackedUint Reserved;
        public PackedUint Userdata0;
        public PackedUint Userdata1;

        public Palette ColorEndpoints;
        public Palette ColorSelectors;

        public Palette AlphaEndpoints;
        public Palette AlphaSelectors;
        
        // crn_packed_uint<2>    m_tables_size;
        // crn_packed_uint<3>    m_tables_ofs;
        public PackedUint TablesSize;
        public PackedUint TablesOfs;
        
        // m_level_ofs[] is actually an array of offsets: m_level_ofs[m_levels]
        // crn_packed_uint<4>    m_level_ofs[1];
        public PackedUint[] LevelOfs;

        public UInt32 BytesPerBlock => (CrnFmt)(UInt32)Format switch
        {
            CrnFmt.DXT1 => 8,
            CrnFmt.DXT5A => 8,
            CrnFmt.ETC1 => 8,
            CrnFmt.ETC2 => 8,
            CrnFmt.ETC1S => 8,
            _ => 16,
        };

        public Header(ReadOnlySpan<byte> data)
        {
            if (data.Length < HeaderMinSize)
                throw new Exception("Header data is too small");
            
            Sig = new PackedUint(data[0..], 2);
            if (Sig != CRNSIG_VALUE)
                throw new Exception("Invalid CRN signature");
            HeaderSize = new PackedUint(data[2..], 2);
            if (HeaderSize < HeaderMinSize || HeaderSize > data.Length)
                throw new Exception("Invalid CRN header size");
            HeaderCrc16 = new PackedUint(data[4..], 2);
            
            DataSize = new PackedUint(data[6..], 4);
            DataCrc16 = new PackedUint(data[10..], 2);
            
            Width = new PackedUint(data[12..], 2);
            Height = new PackedUint(data[14..], 2);
            
            Levels = new PackedUint(data[16..], 1);
            Faces = new PackedUint(data[17..], 1);
            
            Format = new PackedUint(data[18..], 1);
            Flags = new PackedUint(data[19..], 2);
            
            Reserved = new PackedUint(data[21..], 4);
            Userdata0 = new PackedUint(data[25..], 4);
            Userdata1 = new PackedUint(data[29..], 4);
            
            ColorEndpoints = new Palette(data[33..]);
            ColorSelectors = new Palette(data[41..]);
            
            AlphaEndpoints = new Palette(data[49..]);
            AlphaSelectors = new Palette(data[57..]);
            
            TablesSize = new PackedUint(data[65..], 2);
            TablesOfs = new PackedUint(data[67..], 3);
            
            LevelOfs = new PackedUint[Levels];
            for (int i = 0; i < Levels; i++)
            {
                LevelOfs[i] = new PackedUint(data[(70 + i * 4)..], 4);
            }
        }
    }
}