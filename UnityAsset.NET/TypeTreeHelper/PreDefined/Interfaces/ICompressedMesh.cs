using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface ICompressedMesh : IPreDefinedInterface
{
    public IPackedBitVector m_Vertices { get; }
    public IPackedBitVector m_UV { get; }
    public IPackedBitVector m_Normals { get; }
    public IPackedBitVector m_Tangents { get; }
    public IPackedBitVector m_Weights { get; }
    public IPackedBitVector m_NormalSigns { get; }
    public IPackedBitVector m_TangentSigns { get; }
    public IPackedBitVector m_FloatColors { get; }
    public IPackedBitVector m_BoneIndices { get; }
    public IPackedBitVector m_Triangles { get; }
    public UInt32 m_UVInfo { get; }
}