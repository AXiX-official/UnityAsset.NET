using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IVertexData : IPreDefinedInterface
{
    public UInt32 m_VertexCount { get; }
    public List<ChannelInfo> m_Channels { get; }
    public TypelessData m_DataSize { get; }
}