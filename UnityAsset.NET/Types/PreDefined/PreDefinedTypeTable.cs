using UnityAsset.NET.IO;
using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined;

internal sealed record PreDefinedType(Type Type, Func<IReader, object> Create, Action<IWriter, object> Write);

internal static class PreDefinedTypeTable
{
    public static readonly IReadOnlyDictionary<string, PreDefinedType> ByUnityTypeName = Build(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, PreDefinedType> ByExactName = Build(StringComparer.Ordinal);

    private static Dictionary<string, PreDefinedType> Build(StringComparer comparer) => new(comparer)
    {
        ["BoneWeights4"] = new(typeof(BoneWeights4), r => new BoneWeights4(r), (w, v) => ((BoneWeights4)v).Write(w)),
        ["ChannelInfo"] = new(typeof(ChannelInfo), r => new ChannelInfo(r), (w, v) => ((ChannelInfo)v).Write(w)),
        ["GameObject"] = new(typeof(GameObject), r => new GameObject(r), (w, v) => ((GameObject)v).Write(w)),
        ["GUID"] = new(typeof(GUID), r => new GUID(r), (w, v) => ((GUID)v).Write(w)),
        ["Quaternionf"] = new(typeof(Quaternionf), r => new Quaternionf(r), (w, v) => ((Quaternionf)v).Write(w)),
        ["Rectf"] = new(typeof(Rectf), r => new Rectf(r), (w, v) => ((Rectf)v).Write(w)),
        ["SecondarySpriteTexture"] = new(typeof(SecondarySpriteTexture), r => new SecondarySpriteTexture(r), (w, v) => ((SecondarySpriteTexture)v).Write(w)),
        ["SpriteAtlas"] = new(typeof(SpriteAtlas), r => new SpriteAtlas(r), (w, v) => ((SpriteAtlas)v).Write(w)),
        ["SpriteAtlasData"] = new(typeof(SpriteAtlasData), r => new SpriteAtlasData(r), (w, v) => ((SpriteAtlasData)v).Write(w)),
        ["StreamingInfo"] = new(typeof(StreamingInfo), r => new StreamingInfo(r), (w, v) => ((StreamingInfo)v).Write(w)),
        ["TypelessData"] = new(typeof(TypelessData), r => new TypelessData(r), (w, v) => ((TypelessData)v).Write(w)),
        ["Vector2f"] = new(typeof(Vector2f), r => new Vector2f(r), (w, v) => ((Vector2f)v).Write(w)),
        ["Vector3f"] = new(typeof(Vector3f), r => new Vector3f(r), (w, v) => ((Vector3f)v).Write(w)),
        ["Vector4f"] = new(typeof(Vector4f), r => new Vector4f(r), (w, v) => ((Vector4f)v).Write(w)),
    };

    public static bool TryCreate(string typeName, IReader reader, out object value)
    {
        if (ByExactName.TryGetValue(typeName, out var predefined))
        {
            value = predefined.Create(reader);
            return true;
        }

        value = null!;
        return false;
    }

    public static bool TryWrite(string typeName, IWriter writer, object value)
    {
        if (!ByExactName.TryGetValue(typeName, out var predefined) || !predefined.Type.IsInstanceOfType(value))
            return false;

        predefined.Write(writer, value);
        return true;
    }
}
