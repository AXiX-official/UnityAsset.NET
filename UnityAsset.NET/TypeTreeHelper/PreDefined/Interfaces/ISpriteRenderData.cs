using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface ISpriteRenderData : IPreDefinedInterface
{
    public List<SubMesh> m_SubMeshes { get; }
    public List<byte> m_IndexBuffer { get; }
    public IVertexData m_VertexData { get; }
    public Rectf textureRect { get; }
    public Vector2f textureRectOffset { get; }
    public Vector2f atlasRectOffset { get; }
    public UInt32 settingsRaw { get; }
    public Vector4f uvTransform { get; }
    public float downscaleMultiplier { get; }
}