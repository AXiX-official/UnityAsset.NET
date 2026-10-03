namespace UnityAsset.NET.Types.PreDefined;

public interface IUnityAsset : IUnityObject
{
    IUnityAsset Clone() => UnityAsset.NET.Types.AssetCloner.Clone(this);
}
