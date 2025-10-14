using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IRenderer : IComponent
{
    public bool m_Enabled { get; }
    public byte m_CastShadows { get; }
    public byte m_ReceiveShadows { get; }
    public byte? m_DynamicOccludee { get; }
    public byte? m_StaticShadowCaster { get; }
    public byte m_MotionVectors { get; }
    public byte m_LightProbeUsage { get; }
    public byte m_ReflectionProbeUsage { get; }
    public byte? m_RayTracingMode { get; }
    public byte? m_RayTraceProcedural { get; }
    public byte? m_RayTracingAccelStructBuildFlagsOverride { get; }
    public byte? m_RayTracingAccelStructBuildFlags { get; }
    public byte? m_SmallMeshCulling { get; }
    public UInt32? m_RenderingLayerMask { get; }
    public Int32? m_RendererPriority { get; }
    public UInt16 m_LightmapIndex { get; }
    public UInt16 m_LightmapIndexDynamic { get; }
    public Vector4f m_LightmapTilingOffset { get; }
    public Vector4f m_LightmapTilingOffsetDynamic { get; }
    public List<PPtr<IMaterial>> m_Materials { get; }
    public StaticBatchInfo m_StaticBatchInfo { get; }
    public PPtr<Transform> m_StaticBatchRoot { get; }
    public PPtr<Transform>? m_ProbeAnchor { get; }
    public PPtr<IGameObject>? m_LightProbeVolumeOverride { get; }
    public Int32 m_SortingLayerID { get; }
    public Int16 m_SortingLayer { get; }
}