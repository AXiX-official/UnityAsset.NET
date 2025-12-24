using UnityAsset.NET.TypeTree.PreDefined.Types;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public interface IAnimationCurve : IPreDefinedInterface
{
    public List<IKeyframe<float>> m_Curve { get; }
    public int m_PreInfinity { get; }
    public int m_PostInfinity { get; }
    public int m_RotationOrder { get; }
}