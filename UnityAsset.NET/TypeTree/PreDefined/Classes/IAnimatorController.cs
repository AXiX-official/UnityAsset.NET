using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IAnimatorController : IRuntimeAnimatorController
{
    public ValueTuple<uint, string>[] m_TOS { get; }
    public PPtr<IAnimationClip>[] m_AnimationClips { get; }
}