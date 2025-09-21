using System.Text;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SpriteAtlas : IPreDefinedType, INamedAsset
{
    public string ClassName => "SpriteAtlas";
    public string m_Name { get; }
    public List<PPtr<ISprite>> m_PackedSprites { get; }
    public List<string> m_PackedSpriteNamesToIndex { get; }
    public Dictionary<KeyValuePair<GUID, long>, SpriteAtlasData> m_RenderDataMap;
    public string m_Tag { get; }
    public bool m_IsVariant;
    
    public SpriteAtlas(IReader reader, UnityRevision version)
    {
        m_Name = reader.ReadSizedString();
        reader.Align(4);
        m_PackedSprites = reader.ReadListWithAlign(reader.ReadInt32(), r => new PPtr<ISprite>(r), false);

        m_PackedSpriteNamesToIndex = reader.ReadListWithAlign(reader.ReadInt32(), r =>
        {
            var s = r.ReadSizedString();
            r.Align(4);
            return s;
        }, false);

        var m_RenderDataMapSize = reader.ReadInt32();
        m_RenderDataMap = new Dictionary<KeyValuePair<GUID, long>, SpriteAtlasData>();
        for (int i = 0; i < m_RenderDataMapSize; i++)
        {
            var first = new GUID(reader);
            var second = reader.ReadInt64();
            var value = new SpriteAtlasData(reader, version);
            m_RenderDataMap.Add(new KeyValuePair<GUID, long>(first, second), value);
        }
        m_Tag = reader.ReadSizedString();
        reader.Align(4);
        m_IsVariant = reader.ReadBoolean();
        reader.Align(4);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        return sb;
    }
}

public class SpriteAtlasData
{
    public PPtr<ITexture2D> texture;
    public PPtr<ITexture2D> alphaTexture;
    public Rectf textureRect;
    public Vector2f textureRectOffset;
    public Vector2f? atlasRectOffset;
    public Vector4f uvTransform;
    public float downscaleMultiplier;
    public UInt32 settingsRaw;
    public List<SecondarySpriteTexture>? secondaryTextures;

    public SpriteAtlasData(IReader reader, UnityRevision version)
    {
        texture = new PPtr<ITexture2D>(reader);
        alphaTexture = new PPtr<ITexture2D>(reader);
        textureRect = new Rectf(reader);
        textureRectOffset = new Vector2f(reader);
        if (version >= "2017.2") //2017.2 and up
        {
            atlasRectOffset =new Vector2f(reader);
        }
        uvTransform = new Vector4f(reader);
        downscaleMultiplier = reader.ReadFloat();
        settingsRaw = reader.ReadUInt32();
        if (version >= "2020.2") //2020.2 and up
        {
            secondaryTextures =
                reader.ReadListWithAlign(reader.ReadInt32(), r => new SecondarySpriteTexture(r), true);
        }
    }
}