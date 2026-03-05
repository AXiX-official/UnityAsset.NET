using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IMonoBehaviour : INamedObject
{
    public PPtr<GameObject> m_GameObject { get; }
    public byte m_Enabled { get; }
    public PPtr<IMonoScript> m_Script { get; }
}