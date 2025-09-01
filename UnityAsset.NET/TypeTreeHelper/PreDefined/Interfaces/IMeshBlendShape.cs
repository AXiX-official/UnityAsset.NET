using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IMeshBlendShape  : IPreDefinedInterface
{
    public UInt32 firstVertex { get; }
    public UInt32 vertexCount { get; }
    public bool hasNormals { get; }
    public bool hasTangents { get; }
}