namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IGameObject : IAsset
{
    // need more example
    public List<IUnityType> m_Component { get; }

    public UInt32 m_Layer { get; }

    public string m_Name { get; }
}