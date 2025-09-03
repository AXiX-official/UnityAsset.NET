using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IPackedBitVector : IPreDefinedInterface
{
    public UInt32 m_NumItems { get; }
    public List<byte> m_Data { get; }
}