using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PackedUint
    {
        public UInt32 N;
        private fixed byte data[4];

        public PackedUint(UInt32 value, UInt32 n)
        {
            Debug.Assert(n > 0 && n <= 4);
            N = n;
            
            value <<= (int)(4 - n) * 8;
            for (int i = 0; i < n; i++)
            {
                data[i] = (byte)(value >> 24);
                value <<= 8;
            }
        }
        
        public PackedUint(ReadOnlySpan<byte> data, UInt32 n)
        {
            Debug.Assert(n > 0 && n <= 4);
            if (data.Length < n)
                throw new Exception("data length is less than n");
            N = n;
            switch (n)
            {
                case 4: this.data[3] = data[3]; goto case 3;
                case 3: this.data[2] = data[2]; goto case 2;
                case 2: this.data[1] = data[1]; goto case 1;
                case 1: this.data[0] = data[0]; break;
            }
        }
        
        public static implicit operator UInt32(PackedUint value)
        {
            return value.N switch
            {
                1 => value.data[0],
                2 => (UInt32)(value.data[0] << 8 | value.data[1]),
                3 => (UInt32)(value.data[0] << 16 | value.data[1] << 8 | value.data[2]),
                4 => (UInt32)(value.data[0] << 24 | value.data[1] << 16 | value.data[2] << 8 | value.data[3]),
                _ => throw new Exception("Packed integer can hold a 4 byte buffer at max!"),
            };
        }
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct EndianPackedUint
    {
        private fixed byte data[4];
        
        private UInt32 Value
        {
            get
            {
                fixed (byte* ptr = data)
                {
                    return *(UInt32*)ptr;
                }
            }
            set
            {
                fixed (byte* ptr = data)
                {
                    *(UInt32*)ptr = value;
                }
            }
        }

        public byte this[int index]
        {
            get => data[index];
            set => data[index] = value;
        }

        public EndianPackedUint(UInt32 value)
        {
            Value = value;
        }
        
        public static implicit operator EndianPackedUint(UInt32 value)
        {
            return new EndianPackedUint(value);
        }
        
        public static implicit operator UInt32(EndianPackedUint value)
        {
            return value.Value;
        }
    }
    
    public class Palette {
        public PackedUint Ofs;
        public PackedUint Size;
        public PackedUint Num;

        public Palette(ReadOnlySpan<byte> data)
        {
            Ofs = new PackedUint(data[0..], 3);
            Size = new PackedUint(data[3..], 3);
            Num = new PackedUint(data[6..], 2);
        }
    }
    
    public static byte[][] UnpackLevel(byte[] data, UInt32 levelIndex)
    {
        var unpacker = new Unpacker(data);
        
        var header = unpacker.Header;
        uint levelWidth = Math.Max(1, header.Width);
        uint levelHeight = Math.Max(1, header.Height);
        uint numBlocksX = (levelWidth + 3) >> 2;
        uint numBlocksY = (levelHeight + 3) >> 2;

        uint rowPitch = numBlocksX * header.BytesPerBlock;
        uint faceSize = numBlocksY * rowPitch;
        byte[][] faceData = new byte[header.Faces][];
        for (int i = 0; i < header.Faces; i++)
        {
            faceData[i] = new byte[faceSize];
        }
        
        unpacker.UnpackLevel(faceData, faceSize, rowPitch, levelIndex);
        return faceData;
    }
}