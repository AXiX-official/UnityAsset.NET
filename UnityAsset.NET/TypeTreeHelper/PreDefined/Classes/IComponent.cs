using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IComponent : IAsset
{
    public PPtr<IGameObject> m_GameObject { get; }
}