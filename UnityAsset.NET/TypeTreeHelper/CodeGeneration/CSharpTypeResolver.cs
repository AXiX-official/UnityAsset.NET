using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper.CodeGeneration;

public class CSharpTypeResolver
{
    private readonly List<TypeTreeNode> _allNodes;
    private readonly Dictionary<int, BaseTypeInfo> _cache;
    private readonly Dictionary<string, List<BaseTypeInfo>> _map;

    public CSharpTypeResolver(List<TypeTreeNode> allNodes, Dictionary<int, BaseTypeInfo> cache, Dictionary<string, List<BaseTypeInfo>> map)
    {
        _allNodes = allNodes;
        _cache = cache;
        _map = map;
    }
    
    public BaseTypeInfo Resolve(TypeTreeNode node)
    {
        var hash = node.GetHashCode(_allNodes);
        if (_cache.TryGetValue(hash, out var cachedInfo))
        {
            return cachedInfo;
        }

        var newInfo = CreateMappingInfo(node);
        _cache[hash] = newInfo;

        if (_map.TryGetValue(node.Type, out var mappedInfos))
        {
            mappedInfos.Add(newInfo);
        }
        else
        {
            _map[node.Type] = new List<BaseTypeInfo>([newInfo]);
        }

        return newInfo;
    }
    
    private BaseTypeInfo CreateMappingInfo(TypeTreeNode node)
    {
        if (Helper.IsPrimitive(node.Type))
        {
            return CreatePrimitiveMapping(node);
        }
        
        if (Helper.IsPreDefinedType(node.Type))
        {
             return CreatePredefinedTypeMapping(node);
        }

        if (Helper.IsVector(node, _allNodes))
        {
            return CreateVectorMapping(node);
        }

        if (Helper.IsMap(node, _allNodes))
        {
            return CreateMapMapping(node);
        }

        if (Helper.IsGenericPPtr(node))
        {
            return CreateGenericPPtrMapping(node);
        }

        return CreateComplexTypeMapping(node);
    }
    
    private PrimitiveTypeInfo CreatePrimitiveMapping(TypeTreeNode node)
    {
        return new PrimitiveTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type
        );
    }
    
    private PredefinedTypeInfo CreatePredefinedTypeMapping(TypeTreeNode node)
    {
        return new PredefinedTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type,
            node.Type,
            $"new {node.Type}(reader)"
        );
    }
    
    private VectorTypeInfo CreateVectorMapping(TypeTreeNode node)
    {
        var arrayNode = node.Children(_allNodes)[0];
        var children = arrayNode.Children(_allNodes);
        var sizeNode = children[0];
        var dataNode = children[1];
        var elementInfo = Resolve(dataNode); // Recursively resolve the element type
        
        return new VectorTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type,
            arrayNode.Type,
            arrayNode.Name,
            sizeNode.Type,
            sizeNode.Name,
            elementInfo,
            dataNode.Name,
            $"reader.ReadListWithAlign<{elementInfo.DeclarationName}>(reader.ReadInt32(), r => {elementInfo.ReadLogic.Replace("reader", "r")}, {dataNode.RequiresAlign(_allNodes).ToString().ToLower()})"
        );
    }

    private MapTypeInfo CreateMapMapping(TypeTreeNode node)
    {
        var pairNode = node.Children(_allNodes)[0].Children(_allNodes)[1]; // The "data" node of the map's inner array
        var children = pairNode.Children(_allNodes);
        var keyNode = children[0];
        var valueNode = children[1];
        
        var keyInfo = Resolve(keyNode);
        var valueInfo = Resolve(valueNode);

        return new MapTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type,
            pairNode.Type,
            pairNode.Name,
            keyInfo,
            keyNode.Name,
            valueInfo,
            valueNode.Name,
            $"reader.ReadMapWithAlign(reader.ReadInt32(), r => {keyInfo.ReadLogic.Replace("reader", "r")}, r => {valueInfo.ReadLogic.Replace("reader", "r")}, {keyNode.RequiresAlign(_allNodes).ToString().ToLower()}, {valueNode.RequiresAlign(_allNodes).ToString().ToLower()})"
        );
    }

    private PredefinedTypeInfo CreateGenericPPtrMapping(TypeTreeNode node)
    {
        var genericType = node.Type[5..^1];
        if (genericType == "Object") genericType = "TypeTreeHelper.PreDefined.Types.Object";
        else genericType = GetPredefinedTypeOrInterfaceName(genericType); // temp solve
        return new PredefinedTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type,
            $"PPtr<{genericType}>",
            $"new PPtr<{genericType}>(reader)"
        );
    }

    private ComplexTypeInfo CreateComplexTypeMapping(TypeTreeNode node)
    {
        var hash = node.GetHashCode(_allNodes);
        var concreteTypeName = IdentifierSanitizer.SanitizeName($"{node.Type}_{hash}");
        var interfaceName = $"I{node.Type}";

        var declarationTypeName = PreDefinedHelper.IsPreDefinedInterface(interfaceName) ? interfaceName : concreteTypeName;

        var fields = new List<(string, BaseTypeInfo)>();
        foreach (var fieldNode in node.Children(_allNodes))
        {
            fields.Add((IdentifierSanitizer.SanitizeName(fieldNode.Name) ,Resolve(fieldNode)));
        }
        
        return new ComplexTypeInfo(
            node.RequiresAlign(_allNodes),
            node.Type,
            declarationTypeName,
            GetInterfaceName(node),
            concreteTypeName,
            fields.ToArray()
        );
    }
    
    private string GetInterfaceName(TypeTreeNode current)
    {
        if (current.Level == 0)
        {
            switch (current.Type)
            {
                case "AssetBundle": return "IAssetBundle";
                case "Mesh": return "IMesh";
                case "TextAsset": return "ITextAsset";
                case "Texture2D": return "ITexture2D";
                case "Sprite": return "ISprite";
                default:
                    bool hasNameField = current.Children(_allNodes).Any(node => node.Name == "m_Name");
                    return hasNameField ? "INamedAsset" : "IAsset";
            }
        }

        var interfaceName = $"I{current.Type}";
        return PreDefinedHelper.IsPreDefinedInterface(interfaceName) ? interfaceName : "IUnityType";
    }
    
    private string GetPredefinedTypeOrInterfaceName(string type)
    {
        if (PreDefinedHelper.IsPreDefinedType(type))
            return type;
        switch (type)
        {
            case "AssetBundle": return "IAssetBundle";
            case "Mesh": return "IMesh";
            case "TextAsset": return "ITextAsset";
            case "Texture2D": return "ITexture2D";
            case "Sprite": return "ISprite";
            default:
            {
                var interfaceName = $"I{type}";
                return PreDefinedHelper.IsPreDefinedInterface(interfaceName) ? interfaceName : "IUnityType";
            }
        }
    }
}
