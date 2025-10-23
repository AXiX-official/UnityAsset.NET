using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IRenderer : IComponent
{
    public bool m_Enabled { get; }
    public byte m_CastShadows { get; }
    public byte m_ReceiveShadows { get; }
    public byte? m_DynamicOccludee { get => null; }
    public byte? m_StaticShadowCaster { get => null; }
    public byte m_MotionVectors { get; }
    public byte m_LightProbeUsage { get; }
    public byte m_ReflectionProbeUsage { get; }
    public byte? m_RayTracingMode { get => null; }
    public byte? m_RayTraceProcedural { get => null; }
    public byte? m_RayTracingAccelStructBuildFlagsOverride { get => null; }
    public byte? m_RayTracingAccelStructBuildFlags { get => null; }
    public byte? m_SmallMeshCulling { get => null; }
    public UInt32? m_RenderingLayerMask { get => null; }
    public Int32? m_RendererPriority { get => null; }
    public UInt16 m_LightmapIndex { get; }
    public UInt16 m_LightmapIndexDynamic { get; }
    public Vector4f m_LightmapTilingOffset { get; }
    public Vector4f m_LightmapTilingOffsetDynamic { get; }
    public List<PPtr<IMaterial>> m_Materials { get; }
    public StaticBatchInfo m_StaticBatchInfo { get; }
    public PPtr<Transform> m_StaticBatchRoot { get; }
    public PPtr<Transform>? m_ProbeAnchor { get => null; }
    public PPtr<IGameObject>? m_LightProbeVolumeOverride { get => null; }
    public Int32 m_SortingLayerID { get; }
    public Int16 m_SortingLayer { get; }
}