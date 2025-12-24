using System.Text;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class SpriteAtlasData : ISpriteAtlasData
{
    public string ClassName => "SpriteAtlasData";
    public PPtr<ITexture2D> texture { get; }
    public PPtr<ITexture2D> alphaTexture { get; }
    public Rectf textureRect { get; }
    public Vector2f textureRectOffset { get; }
    public Vector2f? atlasRectOffset { get; }
    public Vector4f uvTransform { get; }
    public float downscaleMultiplier { get; }
    public UInt32 settingsRaw { get; }
    public List<SecondarySpriteTexture>? secondaryTextures { get; }

    public SpriteAtlasData(IReader reader)
    {
        UnityRevision version = ((AssetReader)reader).AssetsFile.Metadata.UnityVersion;
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
    
    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = "SpriteAtlasData"
        };
        root.Children.Add(texture.ToAssetNode("texture")!);
        root.Children.Add(alphaTexture.ToAssetNode("alphaTexture")!);
        root.Children.Add(textureRect.ToAssetNode("textureRect")!);
        root.Children.Add(textureRectOffset.ToAssetNode("textureRectOffset")!);
        if (atlasRectOffset != null)
            root.Children.Add(atlasRectOffset.ToAssetNode("atlasRectOffset")!);
        root.Children.Add(uvTransform.ToAssetNode("uvTransform")!);
        root.Children.Add(new AssetNode { Name = "downscaleMultiplier", TypeName = "float", Value = downscaleMultiplier });
        root.Children.Add(new AssetNode { Name = "settingsRaw", TypeName = "UInt32", Value = settingsRaw });
        if (secondaryTextures != null)
        {
            var secondaryTexturesNode = new AssetNode
            {
                Name = "secondaryTextures",
                TypeName = $"vector",
            };
            foreach (var item in secondaryTextures)
            {
                var itemAssetNode = item.ToAssetNode("item");
                if (itemAssetNode != null)
                {
                    secondaryTexturesNode.Children.Add(itemAssetNode);
                }
            }
            root.Children.Add(secondaryTexturesNode);
        }
        return root;
    }
}