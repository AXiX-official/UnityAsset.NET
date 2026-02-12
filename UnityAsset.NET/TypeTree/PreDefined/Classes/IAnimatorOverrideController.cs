using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IAnimatorOverrideController : IRuntimeAnimatorController
{
    public PPtr<IRuntimeAnimatorController> m_Controller { get; }
    public IAnimationClipOverride[] m_Clips { get; }
}