using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface ISkinnedMeshRenderer : IRenderer
{
    public Int32 m_Quality { get; }
    public bool m_UpdateWhenOffscreen { get; }
    //public bool m_SkinnedMotionVectors { get; }
    public PPtr<IMesh> m_Mesh { get; }
    public List<PPtr<Transform>> m_Bones { get; }
    public List<float> m_BlendShapeWeights { get; }
    //public PPtr<IUnityType> m_RootBone { get; }
    //public AABB m_AABB { get; }
    //public bool m_DirtyAABB { get; }
}