using UnityAsset.NET;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Types;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

internal static class AdapterChecks
{
    static TypeTreeRepr Leaf(string name, string type) => TypeTreeRepr.Create(name, type, [], false);
    static TypeTreeRepr Vector(string name, TypeTreeRepr item) => TypeTreeRepr.Create(name, "vector", [TypeTreeRepr.Create("Array", "Array", [Leaf("size", "int"), item], false)], false);
    static byte[] Bytes(Action<IWriter> action)
    {
        using var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true)) action(writer);
        return stream.ToArray();
    }
    static T Read<T>(string name, TypeTreeRepr schema, byte[] bytes)
    {
        var reader = new MemoryReader(bytes);
        return (T)NodeAdapters.Create(name, new NodeData(reader, schema).Value, schema, reader);
    }
    public static void Run()
    {
        // Same interface, different array ranks in different Unity versions, including an empty array.
        foreach (var nested in new[] { false, true })
        {
            var item = nested ? Vector("data", Leaf("data", "UInt32")) : Leaf("data", "UInt32");
            var schema = TypeTreeRepr.Create("Base", "Shader", [Vector("offsets", item)], false);
            var bytes = Bytes(w => { w.WriteInt32(1); if (nested) w.WriteInt32(1); w.WriteUInt32(7); });
            var shader = Read<IShader>("Shader", schema, bytes);
            if (nested) ((uint[][])shader.offsets.Value)[0][0] = 9;
            else ((uint[])shader.offsets.Value)[0] = 9;
            var roundtrip = Read<IShader>("Shader", schema, Bytes(shader.Write));
            var actual = nested ? ((uint[][])roundtrip.offsets.Value)[0][0] : ((uint[])roundtrip.offsets.Value)[0];
            if (actual != 9) throw new Exception("Variant array mutation lost.");
            _ = Read<IShader>("Shader", schema, Bytes(w => w.WriteInt32(0))).offsets;
        }
        // Duplicate keys must survive: Unity maps are sequences, not dictionaries.
        var pair = TypeTreeRepr.Create("data", "pair", [Leaf("first", "string"), Leaf("second", "string")], true);
        var map = TypeTreeRepr.Create("m_SceneHashes", "map", Vector("unused", pair).SubNodes, false);
        var bundleSchema = TypeTreeRepr.Create("Base", "AssetBundle", [map], false);
        var bundle = Read<IAssetBundle>("AssetBundle", bundleSchema, Bytes(w => {
            w.WriteInt32(2); w.WriteSizedString("key"); w.WriteSizedString("one"); w.Align(4); w.WriteSizedString("key"); w.WriteSizedString("two"); w.Align(4);
        }));
        var hashes = bundle.m_SceneHashes!;
        hashes[1] = ("key", "edited");
        var copy = Read<IAssetBundle>("AssetBundle", bundleSchema, Bytes(bundle.Write)).m_SceneHashes!;
        if (copy.Length != 2 || copy[0].Item2 != "one" || copy[1].Item2 != "edited") throw new Exception("Map round trip failed.");
        // Nested interface mutation must flush without assigning the parent property again.
        var settings = TypeTreeRepr.Create("m_TextureSettings", "GLTextureSettings", [Leaf("m_FilterMode", "int")], false);
        var textureSchema = TypeTreeRepr.Create("Base", "Texture2D", [settings], false);
        var texture = Read<ITexture2D>("Texture2D", textureSchema, Bytes(w => w.WriteInt32(1)));
        texture.m_TextureSettings.m_FilterMode = 2;
        if (Read<ITexture2D>("Texture2D", textureSchema, Bytes(texture.Write)).m_TextureSettings.m_FilterMode != 2) throw new Exception("Nested interface mutation lost.");
        var imageSchema = TypeTreeRepr.Create("Base", "Texture2D", [Leaf("image data", "TypelessData")], false);
        var image = Read<ITexture2D>("Texture2D", imageSchema, Bytes(w => { w.WriteInt32(2); w.WriteBytes(new byte[] { 1, 2 }); }));
        image.image_data.data[1] = 3;
        if (Read<ITexture2D>("Texture2D", imageSchema, Bytes(image.Write)).image_data.data[1] != 3)
            throw new Exception("Sanitized field name did not map to the serialized field.");
        var signedSchema = TypeTreeRepr.Create("Base", "Texture2D", [Leaf("m_CompleteImageSize", "int")], false);
        var signed = Read<ITexture2D>("Texture2D", signedSchema, Bytes(w => w.WriteInt32(-1)));
        if (signed.m_CompleteImageSize != uint.MaxValue) throw new Exception("Signed-to-unsigned conversion lost bits.");
        signed.m_CompleteImageSize = 0x80000000u;
        var signedBytes = Bytes(signed.Write);
        if (new NodeData(new MemoryReader(signedBytes), signedSchema).As<Dictionary<string, NodeData>>()["m_CompleteImageSize"].As<int>() != int.MinValue)
            throw new Exception("Original signed schema was not preserved.");
        foreach (var target in new[] { "Component", "SomeUnknownClass" })
        {
            var pointerSchema = TypeTreeRepr.Create("Base", "PrefabModification", [Vector("m_RemovedComponents", Leaf("data", $"PPtr<{target}>"))], false);
            var prefab = Read<IPrefabModification>("PrefabModification", pointerSchema, Bytes(w => w.WriteInt32(0)));
            var value = prefab.m_RemovedComponents.Value;
            if (target == "Component" && value is not UnityAsset.NET.Types.PreDefined.Types.PPtr<IComponent>[])
                throw new Exception("Specific pointer variant was shadowed by the generic pointer.");
            if (target != "Component" && value is not UnityAsset.NET.Types.PreDefined.Types.PPtr<UnityAsset.NET.Types.PreDefined.IUnityObject>[])
                throw new Exception("Unknown pointer target did not fall back to IUnityObject.");
        }
        Console.WriteLine("PASS: variant ranks, empty arrays, duplicate map keys, nested mutations, signedness and pointer variants.");
    }
}
