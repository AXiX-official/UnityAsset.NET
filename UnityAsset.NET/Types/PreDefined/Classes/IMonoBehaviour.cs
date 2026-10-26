using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IMonoBehaviour : INamedObject
{
    public PPtr<GameObject> m_GameObject { get; set; }
    public byte m_Enabled { get; set; }
    public PPtr<IMonoScript> m_Script { get; set; }
}