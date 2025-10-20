namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IGameObject : INamedAsset
{
    // need more example
    public List<IUnityType> m_Component { get; }

    public UInt32 m_Layer { get; }
}