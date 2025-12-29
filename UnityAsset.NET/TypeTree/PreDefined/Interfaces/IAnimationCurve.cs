using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IAnimationCurve<T> : IPreDefinedInterface
    where T : notnull
{
    public List<IKeyframe<T>> m_Curve { get; }
    public int m_PreInfinity { get; }
    public int m_PostInfinity { get; }
    public int m_RotationOrder { get; }
}