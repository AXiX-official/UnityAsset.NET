using AssetRipper.TextureDecoder.Dxt;
using AssetRipper.TextureDecoder.Etc;
using UnityAsset.NET.Enums;
using UnityAsset.NET.TextureHelper.Crunch;

namespace UnityAsset.NET.TextureHelper;

public class CrunchDecoder
{
    public static int Decompress(byte[] data, int width, int height, TextureFormat format, out byte[] output)
    {
        output = [];
        var faceData = Crunch.Crunch.UnpackLevel(data, 0);

        byte[] firstFace = faceData[0];
        
        return format switch
        {
            TextureFormat.DXT1Crunched => DxtDecoder.DecompressDXT1(firstFace, width, height, out output),
            TextureFormat.DXT5Crunched => DxtDecoder.DecompressDXT5(firstFace, width, height, out output),
            TextureFormat.ETC_RGB4Crunched => EtcDecoder.DecompressETC(firstFace, width, height, out output),
            TextureFormat.ETC2_RGBA8Crunched => EtcDecoder.DecompressETC2A8(firstFace, width, height, out output),
            _ => 0,
        };
    }
}