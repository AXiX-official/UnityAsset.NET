using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IUnityTexEnv : IPreDefinedInterface
{
    public PPtr<IUnityType> m_Texture { get; }

    public Vector2f m_Scale { get; }

    public Vector2f m_Offset { get; }
}