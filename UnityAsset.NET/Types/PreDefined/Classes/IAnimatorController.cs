using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IAnimatorController : IRuntimeAnimatorController
{
    public ValueTuple<uint, string>[] m_TOS { get; }
    public PPtr<IAnimationClip>[] m_AnimationClips { get; }
}