using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface ISprite : INamedAsset
{
    public Rectf m_Rect { get; }
    public Vector2f m_Offset { get; }
    public Vector4f m_Border { get; }
    public float m_PixelsToUnits { get; }
    public Vector2f m_Pivot { get; }
    public UInt32 m_Extrude { get; }
    public bool m_IsPolygon { get; }
    public KeyValuePair<GUID, Int64> m_RenderDataKey { get; }
    public List<string> m_AtlasTags { get; }
    public PPtr<SpriteAtlas> m_SpriteAtlas { get; }
    public ISpriteRenderData m_RD { get; }
    public List<List<Vector2f>> m_PhysicsShape { get; }
}