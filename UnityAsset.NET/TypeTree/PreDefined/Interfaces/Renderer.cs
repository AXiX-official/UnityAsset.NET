using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface Renderer : IComponent
{
    public List<PPtr<IMaterial>> m_Materials { get; }
    public IStaticBatchInfo m_StaticBatchInfo { get; }
}