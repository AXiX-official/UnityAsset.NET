using System.Diagnostics;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public partial interface IVertexData
{
    public List<StreamInfo> GetStreams(UnityRevision version)
    {
        var streamCount = m_Channels.Max(x => x.stream) + 1;
        var streams = new List<StreamInfo>();
        uint offset = 0;
        for (int s = 0; s < streamCount; s++)
        {
            uint chnMask = 0;
            uint stride = 0;
            Debug.Assert(m_Channels.Length <= 32);
            for (int chn = 0; chn < m_Channels.Length; chn++)
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
            offset = (offset + (16u - 1u)) & ~(16u - 1u); // align to 16 bytes  
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