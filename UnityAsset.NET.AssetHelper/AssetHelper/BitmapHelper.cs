using System.Runtime.InteropServices;
using SkiaSharp;

namespace UnityAsset.NET.AssetHelper;

/// <summary>
/// The pixel-buffer operations the texture and sprite helpers need: wrap decoded bytes, flip, rotate, crop, resize and
/// punch a tight mesh out of a sprite.
/// <para>
/// Skia's Bgra8888 with straight alpha is exactly what Unity stores, so a decoded texture needs no conversion and no
/// copy beyond the one that lays the rows out the right way up.
/// </para>
/// </summary>
public static class BitmapHelper
{
    public const SKColorType ColorType = SKColorType.Bgra8888;

    /// <summary>Unity stores straight alpha; premultiplying here would darken every edge twice.</summary>
    public const SKAlphaType AlphaType = SKAlphaType.Unpremul;

    /// <summary>
    /// Wraps a BGRA buffer as a bitmap. <paramref name="flipVertical"/> is on by default because Unity stores a texture
    /// bottom-up, and every consumer of these bitmaps draws it top-down.
    /// </summary>
    public static SKBitmap FromBytes(byte[] bgra, int width, int height, bool flipVertical = true)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, ColorType, AlphaType));
        var pixels = bitmap.GetPixels();
        var stride = width * 4;

        for (var y = 0; y < height; y++)
        {
            // Unity textures are stored bottom-up; a caller that already has them the right way up passes false.
            var sourceRow = (flipVertical ? height - 1 - y : y) * stride;
            Marshal.Copy(bgra, sourceRow, IntPtr.Add(pixels, y * stride), stride);
        }

        return bitmap;
    }

    public static SKBitmap FlipHorizontal(SKBitmap source)
        => Draw(source, source.Width, source.Height, canvas =>
        {
            canvas.Translate(source.Width, 0);
            canvas.Scale(-1, 1);
        });

    public static SKBitmap FlipVertical(SKBitmap source)
        => Draw(source, source.Width, source.Height, canvas =>
        {
            canvas.Translate(0, source.Height);
            canvas.Scale(1, -1);
        });

    /// <summary>Rotates clockwise, which is what the sprite packing flags mean.</summary>
    public static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        var swapped = degrees is 90 or 270;
        var width = swapped ? source.Height : source.Width;
        var height = swapped ? source.Width : source.Height;

        return Draw(source, width, height, canvas =>
        {
            switch (degrees)
            {
                case 90:
                    canvas.Translate(width, 0);
                    canvas.RotateDegrees(90);
                    break;
                case 180:
                    canvas.Translate(width, height);
                    canvas.RotateDegrees(180);
                    break;
                case 270:
                    canvas.Translate(0, height);
                    canvas.RotateDegrees(270);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Only 90, 180 and 270 make sense.");
            }
        });
    }

    public static SKBitmap Resize(SKBitmap source, int width, int height)
        => source.Resize(new SKImageInfo(width, height, source.ColorType, source.AlphaType), SKFilterQuality.High)
           ?? throw new InvalidOperationException(
               $"Fail to resize a {source.Width}x{source.Height} bitmap to {width}x{height}.");

    public static SKBitmap Crop(SKBitmap source, SKRectI rect)
    {
        var result = new SKBitmap(new SKImageInfo(rect.Width, rect.Height, source.ColorType, source.AlphaType));
        if (!source.ExtractSubset(result, rect))
        {
            result.Dispose();
            throw new InvalidOperationException(
                $"Fail to crop {rect} out of a {source.Width}x{source.Height} bitmap.");
        }

        return result;
    }

    /// <summary>Draws the source through a canvas setup into a bitmap of the given size.</summary>
    private static SKBitmap Draw(SKBitmap source, int width, int height, Action<SKCanvas> setup)
    {
        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        setup(canvas);
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
