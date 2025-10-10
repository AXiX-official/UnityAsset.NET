using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface ISpriteRenderData : IPreDefinedInterface
{
    public PPtr<ITexture2D> texture { get; }
    public PPtr<ITexture2D> alphaTexture { get; }
    public List<SecondarySpriteTexture>? secondaryTextures { get; }
    public List<SubMesh> m_SubMeshes { get; }
    public List<byte> m_IndexBuffer { get; }
    public IVertexData m_VertexData { get; }
    public List<Matrix4x4f>? m_Bindpose { get; }
    // public List<BoneWeights4>? m_SourceSkin; lack of test data for //2018.2 down to 2018.*
    public Rectf textureRect { get; }
    public Vector2f textureRectOffset { get; }
    public Vector2f atlasRectOffset { get; }
    public UInt32 settingsRaw { get; }
    public Vector4f uvTransform { get; }
    public float downscaleMultiplier { get; }
}