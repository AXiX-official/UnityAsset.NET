using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface INode : IPreDefinedInterface
{
    public int? m_ParentId { get => null; }
    public int? m_AxesId { get => null; }
}