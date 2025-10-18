using System.Text;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class SpriteAtlas : IPreDefinedType, INamedAsset
{
    public string ClassName => "SpriteAtlas";
    public string m_Name { get; }
    public List<PPtr<ISprite>> m_PackedSprites { get; }
    public List<string> m_PackedSpriteNamesToIndex { get; }
    public List<((GUID, Int64), SpriteAtlasData)> m_RenderDataMap { get; }
    public string m_Tag { get; }
    public bool m_IsVariant { get; }
    
    public SpriteAtlas(IReader reader)
    {
        m_Name = reader.ReadSizedString();
        reader.Align(4);
        m_PackedSprites = reader.ReadListWithAlign(reader.ReadInt32(), r => new PPtr<ISprite>(r), false);

        m_PackedSpriteNamesToIndex = reader.ReadListWithAlign(reader.ReadInt32(), r => r.ReadSizedString(), true);
        
        m_RenderDataMap = reader.ReadListWithAlign(
	        reader.ReadInt32(), 
	        r => r.ReadPairWithAlign(
		        r => r.ReadPairWithAlign<GUID, Int64>(r => new GUID(r), 
			        r => r.ReadInt64(), false, false), 
		        r => new SpriteAtlasData(r), 
		        false, 
		        false)
	        , false);
        m_Tag = reader.ReadSizedString();
        reader.Align(4);
        m_IsVariant = reader.ReadBoolean();
        reader.Align(4);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
	{
		sb ??= new StringBuilder();
		sb.AppendLine($"{indent}{ClassName} {name}");
		var childIndent = $"{indent}\t";
		sb.AppendLine($"{childIndent}string m_Name = \"{this.m_Name}\"");
		sb.AppendLine($"{childIndent}vector m_PackedSprites");
		sb.AppendLine($"{childIndent}	Array Array");
		sb.AppendLine($"{childIndent}	int size =  {(uint)this.m_PackedSprites.Count}");
		for (int im_PackedSprites = 0; im_PackedSprites < this.m_PackedSprites.Count; im_PackedSprites++)
		{
			var m_PackedSpriteschildIndentBackUp = childIndent;
			childIndent = $"{childIndent}\t\t";
			sb.AppendLine($"{childIndent}[{im_PackedSprites}]");
			this.m_PackedSprites[im_PackedSprites]?.ToPlainText("data", sb, childIndent);
			childIndent = m_PackedSpriteschildIndentBackUp;
		}
		sb.AppendLine($"{childIndent}vector m_PackedSpriteNamesToIndex");
		sb.AppendLine($"{childIndent}	Array Array");
		sb.AppendLine($"{childIndent}	int size =  {(uint)this.m_PackedSpriteNamesToIndex.Count}");
		for (int im_PackedSpriteNamesToIndex = 0; im_PackedSpriteNamesToIndex < this.m_PackedSpriteNamesToIndex.Count; im_PackedSpriteNamesToIndex++)
		{
			var m_PackedSpriteNamesToIndexchildIndentBackUp = childIndent;
			childIndent = $"{childIndent}\t\t";
			sb.AppendLine($"{childIndent}[{im_PackedSpriteNamesToIndex}]");
			sb.AppendLine($"{childIndent}string data = \"{this.m_PackedSpriteNamesToIndex[im_PackedSpriteNamesToIndex]}\"");
			childIndent = m_PackedSpriteNamesToIndexchildIndentBackUp;
		}
		sb.AppendLine($"{childIndent}map m_RenderDataMap");
		sb.AppendLine($"{childIndent}	Array Array");
		sb.AppendLine($"{childIndent}	int size =  {(uint)this.m_RenderDataMap.Count}");
		for (int im_RenderDataMap = 0; im_RenderDataMap < this.m_RenderDataMap.Count; im_RenderDataMap++)
		{
			var m_RenderDataMapchildIndentBackUp = childIndent;
			childIndent = $"{childIndent}\t\t";
			sb.AppendLine($"{childIndent}[{im_RenderDataMap}]");
			sb.AppendLine($"{childIndent}pair data");
			var datachildIndentBackUp = childIndent;
			childIndent = $"{childIndent}\t\t";
			sb.AppendLine($"{childIndent}pair first");
			var firstchildIndentBackUp = childIndent;
			childIndent = $"{childIndent}\t\t";
			this.m_RenderDataMap[im_RenderDataMap].Item1.Item1?.ToPlainText("first", sb, childIndent);
			sb.AppendLine($"{childIndent}SInt64 second = {this.m_RenderDataMap[im_RenderDataMap].Item1.Item2}");
			childIndent = firstchildIndentBackUp;
			this.m_RenderDataMap[im_RenderDataMap].Item2.ToPlainText("second", sb, childIndent);
			childIndent = datachildIndentBackUp;
			childIndent = m_RenderDataMapchildIndentBackUp;
		}
		sb.AppendLine($"{childIndent}string m_Tag = \"{this.m_Tag}\"");
		sb.AppendLine($"{childIndent}bool m_IsVariant = {this.m_IsVariant}");
		return sb;
	}
}
