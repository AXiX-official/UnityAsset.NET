using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;

public interface IVertexData : IPreDefinedInterface
{
    public UInt32? m_CurrentChannels { get; }
    public UInt32 m_VertexCount { get; }
    public List<ChannelInfo> m_Channels { get; }
    public TypelessData m_DataSize { get; }

    public List<StreamInfo> GetStreams(UnityRevision version)
    {
        var streamCount = m_Channels.Max(x => x.stream) + 1;
        var streams = new List<StreamInfo>();
        uint offset = 0;
        for (int s = 0; s < streamCount; s++)
        {
            uint chnMask = 0;
            uint stride = 0;
            for (int chn = 0; chn < m_Channels.Count; chn++)
            {
                var m_Channel = m_Channels[chn];
                if (m_Channel.stream == s && m_Channel.dimension > 0)
                {
                    chnMask |= 1u << chn;
                    stride += m_Channel.dimension * MeshHelper.GetFormatSize(MeshHelper.ToVertexFormat(m_Channel.format, version));
                }
            }
            streams.Add(new StreamInfo
            {
                channelMask = chnMask,
                offset = offset,
                stride = stride,
                dividerOp = 0,
                frequency = 0
            });
            offset += m_VertexCount * stride;
            offset = (offset + (16u - 1u)) & ~(16u - 1u);
        }

        return streams;
    }
}

public class StreamInfo
{
    public uint channelMask;
    public uint offset;
    public uint stride;
    public uint align;
    public byte dividerOp;
    public ushort frequency;

    public StreamInfo() { }

    public StreamInfo(IReader reader)
    {
        channelMask = reader.ReadUInt32();
        offset = reader.ReadUInt32();
        stride = reader.ReadByte();
        dividerOp = reader.ReadByte();
        frequency = reader.ReadUInt16();
    }
}