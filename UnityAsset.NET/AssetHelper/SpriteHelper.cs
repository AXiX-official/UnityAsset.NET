using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

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
    
    public static Image<Bgra32> GetImage(AssetManager assetManager, ISprite m_Sprite)
    {
        if (m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
        {
            var spriteAtlasData = m_SpriteAtlas.m_RenderDataMap.Find(rdm => rdm.Item1.Equals(m_Sprite.m_RenderDataKey)).Item2;
            if (spriteAtlasData != null && spriteAtlasData.texture.TryGet(out var m_Texture2D))
            {
                return CutImage(assetManager, m_Sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, (SpriteSettings)spriteAtlasData.settingsRaw);
            }
            throw new Exception("SpriteAtlas RenderDataMap not found");
        }
        else
        {
            if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D))
            {
                return CutImage(assetManager, m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, (SpriteSettings)m_Sprite.m_RD.settingsRaw);
            }
            throw new Exception("Sprite RenderData texture not found");
        }
    }
    
    private static Image<Bgra32> CutImage(AssetManager assetManager, ISprite m_Sprite, ITexture2D m_Texture2D, Rectf textureRect, Vector2f textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
    {
        using var originalImage = assetManager.DecodeTexture2DToImage(m_Texture2D, false);
        
        if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
        {
            var width = (int)(m_Texture2D.m_Width / downscaleMultiplier);
            var height = (int)(m_Texture2D.m_Height / downscaleMultiplier);
            originalImage.Mutate(x => x.Resize(width, height));
        }
        var rectX = (int)Math.Floor(textureRect.x);
        var rectY = (int)Math.Floor(textureRect.y);
        var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
        var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
        rectRight = Math.Min(rectRight, originalImage.Width);
        rectBottom = Math.Min(rectBottom, originalImage.Height);
        var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
        var spriteImage = originalImage.Clone(x => x.Crop(rect));
        if (settingsRaw.packed == 1)
        {
            //RotateAndFlip
            switch (settingsRaw.packingRotation)
            {
                case SpritePackingRotation.FlipHorizontal:
                    spriteImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                    break;
                case SpritePackingRotation.FlipVertical:
                    spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                    break;
                case SpritePackingRotation.Rotate180:
                    spriteImage.Mutate(x => x.Rotate(180));
                    break;
                case SpritePackingRotation.Rotate90:
                    spriteImage.Mutate(x => x.Rotate(270));
                    break;
            }
        }

        //Tight
        if (settingsRaw.packingMode == SpritePackingMode.Tight)
        {
            try
            {
                var triangles = GetTriangles(m_Sprite.m_RD, assetManager.Version!);
                var polygons = triangles.Select(x => new Polygon(new LinearLineSegment(x.Select(y => new PointF(y.x, y.y)).ToArray()))).ToArray();
                IPathCollection path = new PathCollection(polygons);
                var matrix = Matrix3x2.CreateScale(m_Sprite.m_PixelsToUnits);
                matrix *= Matrix3x2.CreateTranslation(m_Sprite.m_Rect.width * m_Sprite.m_Pivot.x - textureRectOffset.x, m_Sprite.m_Rect.height * m_Sprite.m_Pivot.y - textureRectOffset.y);
                path = path.Transform(matrix);
                var options = new DrawingOptions
                {
                    GraphicsOptions = new GraphicsOptions
                    {
                        Antialias = false,
                        AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
                    }
                };
                using var mask = new Image<Bgra32>(rect.Width, rect.Height, SixLabors.ImageSharp.Color.Black);
                mask.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, path));
                var bursh = new ImageBrush(mask);
                spriteImage.Mutate(x => x.Fill(options, bursh));
                spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                return spriteImage;
            }
            catch
            {
                // ignored
            }
        }

        //Rectangle
        spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
        return spriteImage;
    }
    
    private static Vector2f[][] GetTriangles(ISpriteRenderData m_RD, UnityRevision version)
    {
        var triangles = new List<Vector2f[]>();
        var m_VertexData = m_RD.m_VertexData;
        var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
        // TODO: m_Streams Interface for IVertexData
        var m_Streams = m_VertexData.GetStreams(version);
        var m_Stream = m_Streams[m_Channel.stream];
        using var vertexReader =
            new CustomStreamReader(new MemoryStream(m_VertexData.m_DataSize.data), Endianness.LittleEndian);
        using var indexReader = new CustomStreamReader(new MemoryStream(m_RD.m_IndexBuffer.ToArray()), Endianness.LittleEndian);
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