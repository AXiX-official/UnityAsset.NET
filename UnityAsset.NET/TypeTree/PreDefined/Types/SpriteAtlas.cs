using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class SpriteAtlas : ISpriteAtlas
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

    public AssetNode? ToAssetNode(string name = "Base")
    {
	    var root = new AssetNode
	    {
		    Name = name,
		    TypeName = "SpriteAtlas"
	    };
	    root.Children.Add(new AssetNode { Name = "m_Name", TypeName = "string", Value = m_Name });
	    
	    var m_PackedSpritesNode = new AssetNode
	    {
		    Name = "m_PackedSprites",
		    TypeName = $"vector",
	    };
	    foreach (var item in m_PackedSprites)
	    {
		    var itemAssetNode = item.ToAssetNode("item");
		    if (itemAssetNode != null)
		    {
			    m_PackedSpritesNode.Children.Add(itemAssetNode);
		    }
	    }
	    root.Children.Add(m_PackedSpritesNode);
	    
	    var m_PackedSpriteNamesToIndexNode = new AssetNode
	    {
		    Name = "m_PackedSpriteNamesToIndex",
		    TypeName = $"vector",
	    };
	    foreach (var item in m_PackedSpriteNamesToIndex)
	    {
		    m_PackedSpriteNamesToIndexNode.Children.Add(new AssetNode { Name = "data", TypeName = "string", Value = item });
	    }
	    root.Children.Add(m_PackedSpriteNamesToIndexNode);
	    
	    var m_RenderDataMapNode = new AssetNode
	    {
		    Name = "m_RenderDataMap",
		    TypeName = $"vector",
	    };
	    foreach (var item in m_RenderDataMap)
	    {
		    var itemAssetNode = new AssetNode
		    {
			    Name = "data",
			    TypeName = "pair",
			    Children =
			    {
				    new AssetNode
				    {
					    Name = "first",
					    TypeName = "pair",
					    Children =
					    {
						    new AssetNode { Name = "first", TypeName = "GUID", Value = item.Item1.Item1 },
						    new AssetNode { Name = "second", TypeName = "Int64", Value = item.Item1.Item2 },
					    }
				    },
				    new AssetNode
				    {
					    Name = "second",
					    TypeName = "SpriteAtlasData",
					    Value = item.Item2
				    }
			    }
		    };
		    m_RenderDataMapNode.Children.Add(itemAssetNode);
	    }
	    root.Children.Add(m_RenderDataMapNode);
	    root.Children.Add(new AssetNode { Name= "m_Tag", TypeName = "string", Value = m_Tag }); 
	    root.Children.Add(new AssetNode { Name= "m_IsVariant", TypeName = "bool", Value = m_IsVariant });
	    
	    return root;
    }
}
