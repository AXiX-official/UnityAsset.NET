using System.Collections;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IMesh : INamedAsset
{
    public List<SubMesh> m_SubMeshes { get; }
    public IBlendShapeData m_Shapes { get; }
    public List<Matrix4x4f> m_BindPose { get; }
    public List<UInt32> m_BoneNameHashes { get; }
    public UInt32 m_RootBoneNameHash { get; }
    public List<MinMaxAABB>? m_BonesAABB { get; }
    public VariableBoneCountWeights? m_VariableBoneCountWeights { get; }
    public byte m_MeshCompression { get; }
    public bool m_IsReadable { get; }
    public bool m_KeepVertices { get; }
    public bool m_KeepIndices { get; }
    public Int32? m_IndexFormat { get; }
    public List<byte> m_IndexBuffer { get; }
    // public List<BoneWeights4> m_Skin; lack of test data for 2018.2 down
    public IVertexData m_VertexData { get; }
    public ICompressedMesh m_CompressedMesh { get; }
    public AABB m_LocalAABB { get; }
    public Int32 m_MeshUsageFlags { get; }
    public Int32? m_CookingOptions { get; }
    public List<byte> m_BakedConvexCollisionMesh { get; }
    public List<byte> m_BakedTriangleCollisionMesh { get; }
    public float? m_MeshMetrics_0_ { get; }
    public float? m_MeshMetrics_1_ { get; }
    public StreamingInfo? m_StreamData { get; }

    private void ProcessData()
    {
        if (this.TryGetPropertyByOriginalName<StreamingInfo>("m_StreamData", out var m_StreamData))
        {
            if (!string.IsNullOrEmpty(m_StreamData.path))
            {
                if (m_VertexData.m_VertexCount > 0)
                {
                    
                }
            }
        }
    }

    private void ReadVertexData(UnityRevision version)
    {
        var m_VertexCount = m_VertexData.m_VertexCount;
        for (var chn = 0; chn < m_VertexData.m_Channels.Count; chn++)
        {
            var m_Channel = m_VertexData.m_Channels[chn];
            var m_VertexData_Streams = m_VertexData.GetStreams(version);
            if (m_Channel.dimension > 0)
            {
                var dimension = m_Channel.dimension;
                var m_Stream = m_VertexData_Streams[m_Channel.stream];
                var channelMask = new BitArray([(int)m_Stream.channelMask]);
                if (channelMask.Get(chn))
                {
                    if (version.Major < 2018 && chn == 2 && m_Channel.format == 2) //kShaderChannelColor && kChannelFormatColor
                    {
                        dimension = 4;
                    }
                    
                    var vertexFormat = MeshHelper.ToVertexFormat(m_Channel.format, version);
                    var componentByteSize = (int)MeshHelper.GetFormatSize(vertexFormat);
                    var componentBytes = new byte[m_VertexCount * m_Channel.dimension * componentByteSize];
                    for (int v = 0; v < m_VertexCount; v++)
                    {
                        var vertexOffset = (int)m_Stream.offset + m_Channel.offset + (int)m_Stream.stride * v;
                        for (int d = 0; d < m_Channel.dimension; d++)
                        {
                            var componentOffset = vertexOffset + componentByteSize * d;
                            Buffer.BlockCopy(m_VertexData.m_DataSize.data, componentOffset, componentBytes, componentByteSize * (v * m_Channel.dimension + d), componentByteSize);
                        }
                    }
                }
            }
        }
    }
}