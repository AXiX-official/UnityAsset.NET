using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IMesh : INamedAsset
{
    public List<SubMesh> m_SubMeshes { get; }
    public IBlendShapeData m_Shapes { get; }
    public List<Matrix4x4f> m_BindPose { get; }
    public List<UInt32> m_BoneNameHashes { get; }
    public UInt32 m_RootBoneNameHash { get; }
    //public List<MinMaxAABB> m_BonesAABB { get; }
    //public VariableBoneCountWeights m_VariableBoneCountWeights { get; }
    public byte m_MeshCompression { get; }
    public bool m_IsReadable { get; }
    public bool m_KeepVertices { get; }
    public bool m_KeepIndices { get; }
    public List<byte> m_IndexBuffer { get; }
    public IVertexData m_VertexData { get; }
    public ICompressedMesh m_CompressedMesh { get; }
    public AABB m_LocalAABB { get; }
    public Int32 m_MeshUsageFlags { get; }
    public List<byte> m_BakedConvexCollisionMesh { get; }
    public List<byte> m_BakedTriangleCollisionMesh { get; }
    //public float m_MeshMetrics_0_ { get; }
    //public float m_MeshMetrics_1_ { get; }
    //public StreamingInfo m_StreamData { get; }
}