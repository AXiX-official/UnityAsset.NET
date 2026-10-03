using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Interfaces;

namespace UnityAsset.NET.Types.PreDefined.Types;

public class SpriteAtlas : ISpriteAtlas
{
    public string ClassName => "SpriteAtlas";
    public string m_Name { get; }
    public PPtr<ISprite>[] m_PackedSprites { get; }
    public string[] m_PackedSpriteNamesToIndex { get; }
    public ((GUID, Int64), SpriteAtlasData)[] m_RenderDataMap { get; }
    public string m_Tag { get; }
    public bool m_IsVariant { get; }
    
    public SpriteAtlas(IReader reader)
    {
        m_Name = reader.ReadSizedString();
        reader.Align(4);
        m_PackedSprites = reader.ReadArrayWithAlign(reader.ReadInt32(), r => new PPtr<ISprite>(r), false);

        m_PackedSpriteNamesToIndex = reader.ReadArrayWithAlign(reader.ReadInt32(), r => r.ReadSizedString(), true);
        
        m_RenderDataMap = reader.ReadArrayWithAlign(
	        reader.ReadInt32(), 
	        r0 => r0.ReadPairWithAlign(
		        r1 => r1.ReadPairWithAlign<GUID, Int64>(r => new GUID(r1), 
			        r2 => r2.ReadInt64(), false, false), 
		        r1 => new SpriteAtlasData(r1), 
		        false, 
		        false)
	        , false);
        m_Tag = reader.ReadSizedString();
        reader.Align(4);
        m_IsVariant = reader.ReadBoolean();
        reader.Align(4);
    }
	
    public void Write(IWriter writer)
    {
        writer.WriteSizedString(m_Name);
        writer.Align(4);
        writer.WriteArrayWithAlign(m_PackedSprites, (w0, sprite) => sprite.Write(w0), false);
        writer.WriteArrayWithAlign(m_PackedSpriteNamesToIndex, (w0, index) => w0.WriteSizedString(index), true);
        writer.WriteArrayWithAlign(m_RenderDataMap,
            (w0, entry) => w0.WritePairWithAlign(entry,
                (w1, key) => w1.WritePairWithAlign<GUID, Int64>(key,
                    (w2, guid) => guid.Write(w2),
                    (w2, pathId) => w2.WriteInt64(pathId),
                    false, false),
                (w1, data) => data.Write(w1),
                false, false),
            false);
        writer.WriteSizedString(m_Tag);
        writer.Align(4);
        writer.WriteBoolean(m_IsVariant);
        writer.Align(4);
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

