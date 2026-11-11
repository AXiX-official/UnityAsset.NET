using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET.AssetHelper.TextureHelper;

public static class CrunchDecoder
{
    public static int Decompress(byte[] data, int width, int height, TextureFormat format, out byte[] output)
    {
        output = [];
        var faceData = Crunch.Crunch.UnpackLevel(data, 0);

        byte[] firstFace = faceData[0];
        
        return format switch
        {
#if NET9_0_OR_GREATER
            TextureFormat.DXT1Crunched => DxtDecoder.DecompressDXT1<ColorBGRA32, byte>(firstFace, width, height, out output),
            TextureFormat.DXT5Crunched => DxtDecoder.DecompressDXT5<ColorBGRA32, byte>(firstFace, width, height, out output),
            TextureFormat.ETC_RGB4Crunched => EtcDecoder.DecompressETC<ColorBGRA32, byte>(firstFace, width, height, out output),
            TextureFormat.ETC2_RGBA8Crunched => EtcDecoder.DecompressETC2A8<ColorBGRA32, byte>(firstFace, width, height, out output),
#else
            TextureFormat.DXT1Crunched => DxtDecoder.DecompressDXT1(firstFace, width, height, out output),
            TextureFormat.DXT5Crunched => DxtDecoder.DecompressDXT5(firstFace, width, height, out output),
            TextureFormat.ETC_RGB4Crunched => EtcDecoder.DecompressETC(firstFace, width, height, out output),
            TextureFormat.ETC2_RGBA8Crunched => EtcDecoder.DecompressETC2A8(firstFace, width, height, out output),
#endif
            _ => 0,
        };
    }
}