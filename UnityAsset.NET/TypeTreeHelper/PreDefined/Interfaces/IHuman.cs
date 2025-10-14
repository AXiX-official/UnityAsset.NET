using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IHuman : IPreDefinedInterface
{
    public xform m_RootX { get; }
    public IUnityType m_Skeleton { get; }
    public IUnityType m_SkeletonPose { get; }
    public IUnityType m_LeftHand { get; }
    public IUnityType m_RightHand { get; }
    // public List<Handle> m_Handles;
    // public List<Collider> m_ColliderArray;
    public List<Int32> m_HumanBoneIndex { get; }
    public List<float> m_HumanBoneMass { get; }
    // public int[] m_ColliderIndex;
    public float m_Scale { get; }
    public float m_ArmTwist { get; }
    public float m_ForeArmTwist { get; }
    public float m_UpperLegTwist { get; }
    public float m_LegTwist { get; }
    public float m_ArmStretch { get; }
    public float m_LegStretch { get; }
    public float m_FeetSpacing { get; }
    public bool m_HasLeftHand { get; }
    public bool m_HasRightHand { get; }
    public bool m_HasTDoF { get; }
}