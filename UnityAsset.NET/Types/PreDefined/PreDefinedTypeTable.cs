using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined;

internal sealed record PreDefinedType(Type Type, Func<IReader, object> Create);

internal static class PreDefinedTypeTable
{
    public static readonly IReadOnlyDictionary<string, PreDefinedType> ByUnityTypeName =
        new Dictionary<string, PreDefinedType>(StringComparer.OrdinalIgnoreCase)
    {
        ["BoneWeights4"] = new(typeof(BoneWeights4), reader => new BoneWeights4(reader)),
        ["ChannelInfo"] = new(typeof(ChannelInfo), reader => new ChannelInfo(reader)),
        ["GameObject"] = new(typeof(GameObject), reader => new GameObject(reader)),
        ["GUID"] = new(typeof(GUID), reader => new GUID(reader)),
        ["Quaternionf"] = new(typeof(Quaternionf), reader => new Quaternionf(reader)),
        ["Rectf"] = new(typeof(Rectf), reader => new Rectf(reader)),
        ["SecondarySpriteTexture"] = new(typeof(SecondarySpriteTexture), reader => new SecondarySpriteTexture(reader)),
        ["SpriteAtlas"] = new(typeof(SpriteAtlas), reader => new SpriteAtlas(reader)),
        ["SpriteAtlasData"] = new(typeof(SpriteAtlasData), reader => new SpriteAtlasData(reader)),
        ["StreamingInfo"] = new(typeof(StreamingInfo), reader => new StreamingInfo(reader)),
        ["TypelessData"] = new(typeof(TypelessData), reader => new TypelessData(reader)),
        ["Vector2f"] = new(typeof(Vector2f), reader => new Vector2f(reader)),
        ["Vector3f"] = new(typeof(Vector3f), reader => new Vector3f(reader)),
        ["Vector4f"] = new(typeof(Vector4f), reader => new Vector4f(reader)),
    };
}
