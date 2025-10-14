using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IAnimator : IBehaviour
{
    public PPtr<IAvatar> m_Avatar { get; }
    //public PPtr<IUnityType> m_Controller { get; }
    public Int32 m_CullingMode { get; }
    public Int32 m_UpdateMode { get; }
    public bool m_ApplyRootMotion { get; }
    public bool m_LinearVelocityBlending { get; }
    public bool? m_StabilizeFeet { get; }
    public bool m_HasTransformHierarchy { get; }
    public bool m_AllowConstantClipSamplingOptimization { get; }
    public bool? m_KeepAnimatorStateOnDisable { get; }
}