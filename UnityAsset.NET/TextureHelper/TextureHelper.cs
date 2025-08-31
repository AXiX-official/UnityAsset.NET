using System.Buffers.Binary;
using AssetRipper.TextureDecoder.Astc;
using AssetRipper.TextureDecoder.Atc;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Pvrtc;
using AssetRipper.TextureDecoder.Rgb;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetRipper.TextureDecoder.Yuy2;
using UnityAsset.NET.Classes;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.TextureHelper;

public static class TextureHelper
{
    private static byte[] SwapBytesForXbox(byte[] input)
    {
        byte[] output = new byte[input.Length];
    
        for (int i = 0; i < input.Length; i += 2)
        {
            if (i + 1 < input.Length)
            {
                output[i] = input[i + 1];
                output[i + 1] = input[i];
            }
            else
            {
                output[i] = input[i];
            }
        }
    
        return output;
    }
    
    public static byte[] Decode(ITexture2D tex, AssetManager assetManager)
    {
        var imageData = tex.image_data.size == 0 ? assetManager.LoadStreamingData(tex.m_StreamData) : tex.image_data.data;
        
        if (imageData == null)
            throw new NullReferenceException();
        
        byte[] output = [];
        var width = tex.m_Width;
        var height = tex.m_Height;
        var format = (TextureFormat)tex.m_TextureFormat;

        if (assetManager.BuildTarget == BuildTarget.XBOX360)
        {
            switch (format)
            {
                case TextureFormat.RGB565:
                case TextureFormat.DXT1:
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                {
                    imageData = SwapBytesForXbox(imageData);
                    break;
                }
            }
        }

        if (assetManager.BuildTarget == BuildTarget.Switch && tex.m_PlatformBlob.Count >= 12)
        {
            var gobsPerBlock = 1 << BinaryPrimitives.ReadInt32LittleEndian(tex.m_PlatformBlob[8..12].ToArray());
            if (gobsPerBlock > 1)
            {
                if (format == TextureFormat.RGB24)
                    format = TextureFormat.RGBA32;
                else if (format == TextureFormat.BGR24)
                    format = TextureFormat.BGRA32;
                
                //
            }
        }
        
        if (format == TextureFormat.BGRA32)
        {
            byte[] newData = new byte[width * height * 4];
            Array.Copy(imageData, newData, width * height * 4);
            return newData;
        }
        
        int size = format switch
        {
            TextureFormat.Alpha8 => RgbConverter.Convert<ColorA<byte>, byte, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.ARGB4444 => RgbConverter.Convert<ColorARGB16, byte, ColorBGRA32, byte>(imageData, width,
                height, out output),
            //TextureFormat.ARGBFloat => not supported :(
            TextureFormat.RGB24 => RgbConverter.Convert<ColorRGB<byte>, byte, ColorBGRA32, byte>(imageData, width,
                height, out output),
            //TextureFormat.BGR24 => not supported :(
            TextureFormat.RGBA32 => RgbConverter.Convert<ColorRGBA<byte>, byte, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGB565 => RgbConverter.Convert<ColorRGB16, byte, ColorBGRA32, byte>(imageData, width, height,
                out output),
            TextureFormat.ARGB32 => RgbConverter.Convert<ColorARGB32, byte, ColorBGRA32, byte>(imageData, width, height,
                out output),
            TextureFormat.R16 => RgbConverter.Convert<ColorR<ushort>, ushort, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGBA4444 => RgbConverter.Convert<ColorRGBA16, byte, ColorBGRA32, byte>(imageData, width,
                height, out output),
            //TextureFormat.BGRA32 => imageData.Length,
            TextureFormat.RG16 => RgbConverter.Convert<ColorRG<byte>, byte, ColorBGRA32, byte>(imageData, width, height,
                out output),
            TextureFormat.R8 => RgbConverter.Convert<ColorR<byte>, byte, ColorBGRA32, byte>(imageData, width, height,
                out output),
            TextureFormat.RHalf => RgbConverter.Convert<ColorR<Half>, Half, ColorBGRA32, byte>(imageData, width, height,
                out output),
            TextureFormat.RGHalf => RgbConverter.Convert<ColorRG<Half>, Half, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGBAHalf => RgbConverter.Convert<ColorRGBA<Half>, Half, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RFloat => RgbConverter.Convert<ColorR<float>, float, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGFloat => RgbConverter.Convert<ColorRG<float>, float, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGBFloat => RgbConverter.Convert<ColorRGB<float>, float, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGBAFloat => RgbConverter.Convert<ColorRGBA<float>, float, ColorBGRA32, byte>(imageData,
                width, height, out output),
            TextureFormat.RGB9e5Float => RgbConverter.Convert<ColorRGB9e5, double, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RG32 => RgbConverter.Convert<ColorRG<ushort>, ushort, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGB48 => RgbConverter.Convert<ColorRGB<ushort>, ushort, ColorBGRA32, byte>(imageData, width,
                height, out output),
            TextureFormat.RGBA64 => RgbConverter.Convert<ColorRGBA<ushort>, ushort, ColorBGRA32, byte>(imageData, width,
                height, out output),

            TextureFormat.DXT1 => DxtDecoder.DecompressDXT1(imageData, width, height, out output),
            TextureFormat.DXT3 => DxtDecoder.DecompressDXT3(imageData, width, height, out output),
            TextureFormat.DXT5 => DxtDecoder.DecompressDXT5(imageData, width, height, out output),
            TextureFormat.BC4 => Bc4.Decompress(imageData, width, height, out output),
            TextureFormat.BC5 => Bc5.Decompress(imageData, width, height, out output),
            TextureFormat.BC6H => Bc6h.Decompress(imageData, width, height, false, out output),
            TextureFormat.BC7 => Bc7.Decompress(imageData, width, height, out output),

            TextureFormat.ETC_RGB4 => EtcDecoder.DecompressETC(imageData, width, height, out output),
            TextureFormat.ETC2_RGB4 => EtcDecoder.DecompressETC2(imageData, width, height, out output),
            TextureFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA => EtcDecoder.DecompressETC2A1(imageData, width, height,
                out output),
            TextureFormat.ETC2_RGBA8 => EtcDecoder.DecompressETC2A8(imageData, width, height, out output),
            TextureFormat.EAC_R => EtcDecoder.DecompressEACRUnsigned(imageData, width, height, out output),
            TextureFormat.EAC_R_SIGNED => EtcDecoder.DecompressEACRSigned(imageData, width, height, out output),
            TextureFormat.EAC_RG => EtcDecoder.DecompressEACRGUnsigned(imageData, width, height, out output),
            TextureFormat.EAC_RG_SIGNED => EtcDecoder.DecompressEACRGSigned(imageData, width, height, out output),

            TextureFormat.ASTC_RGB_4x4 or
                TextureFormat.ASTC_RGBA_4x4 => AstcDecoder.DecodeASTC(imageData, width, height, 4, 4, out output),
            TextureFormat.ASTC_RGB_5x5 or
                TextureFormat.ASTC_RGBA_5x5 => AstcDecoder.DecodeASTC(imageData, width, height, 5, 5, out output),
            TextureFormat.ASTC_RGB_6x6 or
                TextureFormat.ASTC_RGBA_6x6 => AstcDecoder.DecodeASTC(imageData, width, height, 6, 6, out output),
            TextureFormat.ASTC_RGB_8x8 or
                TextureFormat.ASTC_RGBA_8x8 => AstcDecoder.DecodeASTC(imageData, width, height, 8, 8, out output),
            TextureFormat.ASTC_RGB_10x10 or
                TextureFormat.ASTC_RGBA_10x10 => AstcDecoder.DecodeASTC(imageData, width, height, 10, 10, out output),
            TextureFormat.ASTC_RGB_12x12 or
                TextureFormat.ASTC_RGBA_12x12 => AstcDecoder.DecodeASTC(imageData, width, height, 12, 12, out output),

            TextureFormat.ATC_RGB4 => AtcDecoder.DecompressAtcRgb4(imageData, width, height, out output),
            TextureFormat.ATC_RGBA8 => AtcDecoder.DecompressAtcRgba8(imageData, width, height, out output),

            TextureFormat.PVRTC_RGB2 or
                TextureFormat.PVRTC_RGBA2 => PvrtcDecoder.DecompressPVRTC(imageData, width, height, true, out output),
            TextureFormat.PVRTC_RGB4 or
                TextureFormat.PVRTC_RGBA4 => PvrtcDecoder.DecompressPVRTC(imageData, width, height, false, out output),

            TextureFormat.YUY2 => Yuy2Decoder.DecompressYUY2(imageData, width, height, out output),

            TextureFormat.DXT1Crunched => CrunchDecoder.Decompress(imageData, width, height, TextureFormat.DXT1Crunched, out output),
            TextureFormat.DXT5Crunched => CrunchDecoder.Decompress(imageData, width, height, TextureFormat.DXT5Crunched, out output),
            TextureFormat.ETC_RGB4Crunched => CrunchDecoder.Decompress(imageData, width, height, TextureFormat.ETC_RGB4Crunched, out output),

            _ => 0
        };
        return output;
    }
}