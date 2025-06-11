namespace UnityAsset.NET.IO;

public interface ISerializable<T>
{
    public void Serialize(AssetWriter writer);
    public static abstract T Deserialize(AssetReader reader);
}