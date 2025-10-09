using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET;

public static class SpriteHelper
{
    /*public static void GetImage(ISprite m_Sprite)
    {
        if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
        {
            if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
            {
                return CutImage(m_Sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, spriteAtlasData.settingsRaw);
            }
        }
        else
        {
            if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D))
            {
                return CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);
            }
        }
        return null;
    }*/
}