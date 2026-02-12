using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IAnimatorOverrideController : IRuntimeAnimatorController
{
    public PPtr<IRuntimeAnimatorController> m_Controller { get; }
    public List<IAnimationClipOverride> m_Clips { get; }
}