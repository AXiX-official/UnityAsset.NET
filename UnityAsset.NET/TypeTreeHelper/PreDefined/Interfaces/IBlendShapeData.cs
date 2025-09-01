using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IBlendShapeData : IPreDefinedInterface
{
    public List<BlendShapeVertex> vertices { get; }
    public List<IMeshBlendShape> shapes { get; }
    public List<MeshBlendShapeChannel> channels { get; }
    public List<float> fullWeights { get; }
}
