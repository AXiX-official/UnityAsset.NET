using System.Text;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SpriteAtlasData : IPreDefinedType
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
    
    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = $"{indent}\t";
        this.texture?.ToPlainText("texture", sb, childIndent);
        this.alphaTexture?.ToPlainText("alphaTexture", sb, childIndent);
        this.textureRect?.ToPlainText("textureRect", sb, childIndent);
        this.textureRectOffset?.ToPlainText("textureRectOffset", sb, childIndent);
        this.atlasRectOffset?.ToPlainText("atlasRectOffset", sb, childIndent);
        this.uvTransform?.ToPlainText("uvTransform", sb, childIndent);
        sb.AppendLine($"{childIndent}float downscaleMultiplier = {this.downscaleMultiplier}");
        sb.AppendLine($"{childIndent}unsigned int settingsRaw = {this.settingsRaw}");
        sb.AppendLine($"{childIndent}vector secondaryTextures");
        sb.AppendLine($"{childIndent}	Array Array");
        if (secondaryTextures != null)
        {
            sb.AppendLine($"{childIndent}	int size =  {(uint)secondaryTextures.Count}");
            for (int isecondaryTextures = 0; isecondaryTextures < secondaryTextures.Count; isecondaryTextures++)
            {
                var secondaryTextureschildIndentBackUp = childIndent;
                childIndent = $"{childIndent}\t\t";
                sb.AppendLine($"{childIndent}[{isecondaryTextures}]");
                secondaryTextures[isecondaryTextures]?.ToPlainText("data", sb, childIndent);
                childIndent = secondaryTextureschildIndentBackUp;
            }
        }
        return sb;
    }
}