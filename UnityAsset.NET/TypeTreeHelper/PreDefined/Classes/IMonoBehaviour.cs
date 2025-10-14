using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IMonoBehaviour : IBehaviour
{
    public PPtr<IMonoScript> m_Script { get; }

    public string m_Name { get; }
}