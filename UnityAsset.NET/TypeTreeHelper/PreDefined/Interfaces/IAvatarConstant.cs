using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IAvatarConstant : IPreDefinedInterface
{
    public IUnityType m_AvatarSkeleton { get; }
    public IUnityType m_AvatarSkeletonPose { get; }
    public IUnityType m_DefaultPose { get; }
    public List<UInt32> m_SkeletonNameIDArray { get; }
    public IUnityType m_Human { get; }
    public List<Int32> m_HumanSkeletonIndexArray { get; }
    public List<Int32> m_HumanSkeletonReverseIndexArray { get; }
    public Int32 m_RootMotionBoneIndex { get; }
    public xform m_RootMotionBoneX { get; }
    public IUnityType m_RootMotionSkeleton { get; }
    public IUnityType m_RootMotionSkeletonPose { get; }
    public List<Int32> m_RootMotionSkeletonIndexArray { get; }
}