using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface Renderer : IComponent
{
    public PPtr<IMaterial>[] m_Materials { get; }
    public IStaticBatchInfo m_StaticBatchInfo { get; }
}