using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IAnimatorOverrideController : IRuntimeAnimatorController
{
    public PPtr<IRuntimeAnimatorController> m_Controller { get; }
    public IAnimationClipOverride[] m_Clips { get; }
}