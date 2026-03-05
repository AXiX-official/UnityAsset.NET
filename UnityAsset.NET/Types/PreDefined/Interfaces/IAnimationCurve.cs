using UnityAsset.NET.Types.PreDefined.Types;

namespace UnityAsset.NET.Types.PreDefined.Interfaces;

public interface IAnimationCurve<T> : IPreDefinedInterface
    where T : notnull
{
    public IKeyframe<T>[] m_Curve { get; }
    public int m_PreInfinity { get; }
    public int m_PostInfinity { get; }
    public int m_RotationOrder { get; }
}