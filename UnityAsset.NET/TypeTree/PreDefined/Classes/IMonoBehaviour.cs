using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IMonoBehaviour : INamedAsset
{
    public PPtr<IGameObject> m_GameObject { get; }
    public byte m_Enabled { get; }
    public PPtr<IMonoScript> m_Script { get; }
}