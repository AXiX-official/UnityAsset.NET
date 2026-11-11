using SkiaSharp;
using UnityAsset.NET.Files;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Types.Predefined.Interfaces;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.AssetHelper;

public static class SpriteHelper
{
    public enum SpritePackingRotation
    {
        None = 0,
        FlipHorizontal = 1,
        FlipVertical = 2,
        Rotate180 = 3,
        Rotate90 = 4
    }

    public enum SpritePackingMode
    {
        Tight = 0,
        Rectangle
    }

    public enum SpriteMeshType
    {
        FullRect,
        Tight
    }
    
    public class SpriteSettings
    {
        public uint packed;
        public SpritePackingMode packingMode;
        public SpritePackingRotation packingRotation;
        public SpriteMeshType meshType;

        public SpriteSettings(UInt32 settingsRaw)
        {
            packed = settingsRaw & 1; //1
            packingMode = (SpritePackingMode)((settingsRaw >> 1) & 1); //1
            packingRotation = (SpritePackingRotation)((settingsRaw >> 2) & 0xf); //4
            meshType = (SpriteMeshType)((settingsRaw >> 6) & 1); //1
            //reserved
        }
        
        public static explicit operator SpriteSettings(uint settingsRaw)
        {
            return new SpriteSettings(settingsRaw);
        }
    }
    
    public static SKBitmap GetImage(IUnitySession session, ISprite m_Sprite, IVirtualFileInfo? src)
    {
        if (m_Sprite.m_SpriteAtlas.TryGet(session, out var m_SpriteAtlas))
        {
            var spriteAtlasData = Array.Find(m_SpriteAtlas.m_RenderDataMap, rdm => rdm.Item1.Equals(m_Sprite.m_RenderDataKey)).Item2;
            if (spriteAtlasData != null && spriteAtlasData.texture.TryGet(session, out var m_Texture2D))
            {
                return CutImage(session, m_Sprite, m_Texture2D, src, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, (SpriteSettings)spriteAtlasData.settingsRaw);
            }
            throw new Exception("SpriteAtlas RenderDataMap not found");
        }
        else
        {
            if (m_Sprite.m_RD.texture.TryGet(session, out var m_Texture2D))
            {
                return CutImage(session, m_Sprite, m_Texture2D, src, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, (SpriteSettings)m_Sprite.m_RD.settingsRaw);
            }
            throw new Exception("Sprite RenderData texture not found");
        }
    }
    
    private static SKBitmap CutImage(IUnitySession session, ISprite m_Sprite, ITexture2D m_Texture2D, IVirtualFileInfo? src, Rectf textureRect, Vector2f textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
    {
        using var originalImage = session.DecodeTexture2DToBitmap(m_Texture2D, src, false);
        
        SKBitmap? resized = null;
        try
        {
            var source = originalImage;
            if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
            {
                resized = BitmapHelper.Resize(originalImage,
                    (int)(m_Texture2D.m_Width / downscaleMultiplier),
                    (int)(m_Texture2D.m_Height / downscaleMultiplier));
                source = resized;
            }

            var rectX = (int)Math.Floor(textureRect.x);
            var rectY = (int)Math.Floor(textureRect.y);
            var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
            var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
            rectRight = Math.Min(rectRight, source.Width);
            rectBottom = Math.Min(rectBottom, source.Height);

            var spriteImage = BitmapHelper.Crop(source, new SKRectI(rectX, rectY, rectRight, rectBottom));
            try
            {
                if (settingsRaw.packed == 1)
                {
                    //RotateAndFlip
                    var rotated = settingsRaw.packingRotation switch
                    {
                        SpritePackingRotation.FlipHorizontal => BitmapHelper.FlipHorizontal(spriteImage),
                        SpritePackingRotation.FlipVertical => BitmapHelper.FlipVertical(spriteImage),
                        SpritePackingRotation.Rotate180 => BitmapHelper.Rotate(spriteImage, 180),
                        SpritePackingRotation.Rotate90 => BitmapHelper.Rotate(spriteImage, 270),
                        _ => null
                    };

                    if (rotated is not null)
                    {
                        spriteImage.Dispose();
                        spriteImage = rotated;
                    }
                }

                //Tight
                if (settingsRaw.packingMode == SpritePackingMode.Tight)
                {
                    try
                    {
                        PunchOut(session, m_Sprite, spriteImage, textureRectOffset);
                    }
                    catch
                    {
                        // Tight masking is best effort, as it always was: a sprite whose mesh cannot be read is still a
                        // rectangle, and that is better than no sprite at all.
                    }
                }

                var flipped = BitmapHelper.FlipVertical(spriteImage);
                spriteImage.Dispose();
                return flipped;
            }
            catch
            {
                spriteImage.Dispose();
                throw;
            }
        }
        finally
        {
            resized?.Dispose();
        }
    }

    /// <summary>
    /// Removes the area outside the sprite's own triangles, which is what makes a sprite packed in a tight atlas come out
    /// as its silhouette instead of a rectangle. The mesh is in sprite pixels, so it is scaled by pixels-to-units and
    /// moved to the sprite's pivot, exactly as the original drawing did.
    /// </summary>
    private static void PunchOut(IUnitySession session, ISprite m_Sprite, SKBitmap spriteImage, Vector2f textureRectOffset)
    {
        var path = new SKPath();
        try
        {
            foreach (var triangle in GetTriangles(m_Sprite.m_RD, session.Version!))
            {
                path.MoveTo(triangle[0].x, triangle[0].y);
                path.LineTo(triangle[1].x, triangle[1].y);
                path.LineTo(triangle[2].x, triangle[2].y);
                path.Close();
            }

            var matrix = SKMatrix.CreateScale(m_Sprite.m_PixelsToUnits, m_Sprite.m_PixelsToUnits);
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(
                m_Sprite.m_Rect.width * m_Sprite.m_Pivot.x - textureRectOffset.x,
                m_Sprite.m_Rect.height * m_Sprite.m_Pivot.y - textureRectOffset.y));
            path.Transform(matrix);

            using var canvas = new SKCanvas(spriteImage);
            using var paint = new SKPaint
            {
                // DestOut keeps the destination minus the drawn area, which is the punch-out; the antialiasing is off to
                // match the hard mask the previous renderer built.
                BlendMode = SKBlendMode.DstOut,
                IsAntialias = false,
                Color = SKColors.Black
            };
            canvas.DrawPath(path, paint);
        }
        finally
        {
            path.Dispose();
        }
    }
    
    private static Vector2f[][] GetTriangles(ISpriteRenderData m_RD, UnityRevision version)
    {
        var triangles = new List<Vector2f[]>();
        var m_VertexData = m_RD.m_VertexData;
        var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
        // TODO: m_Streams Interface for IVertexData
        var m_Streams = m_VertexData.GetStreams(version);
        var m_Stream = m_Streams[m_Channel.stream];
        var vertexReader = new MemoryReader(m_VertexData.m_DataSize.data, endian: Endianness.LittleEndian);
        var indexReader = new MemoryReader(m_RD.m_IndexBuffer, endian: Endianness.LittleEndian);
        foreach (var subMesh in m_RD.m_SubMeshes)
        {
            vertexReader.Position = m_Stream.offset + subMesh.firstVertex * m_Stream.stride + m_Channel.offset;

            var vertices = new Vector2f[subMesh.vertexCount];
            for (int v = 0; v < subMesh.vertexCount; v++)
            {
                vertices[v] = (Vector2f)new Vector3f(vertexReader);
                vertexReader.Position += m_Stream.stride - 12;
            }

            indexReader.Position = subMesh.firstByte;

            var triangleCount = subMesh.indexCount / 3u;
            for (int i = 0; i < triangleCount; i++)
            {
                var first = ((IReader)indexReader).ReadUInt16() - subMesh.firstVertex;
                var second = ((IReader)indexReader).ReadUInt16() - subMesh.firstVertex;
                var third = ((IReader)indexReader).ReadUInt16() - subMesh.firstVertex;
                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                triangles.Add(triangle);
            }
        }
        return triangles.ToArray();
    }
}