using System.Buffers.Binary;
using System.Collections;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.AssetHelper;

public static class MeshHelper
{
    public enum VertexChannelFormat
    {
        Float,
        Float16,
        Color,
        Byte,
        UInt32
    }

    public enum VertexFormat2017
    {
        Float,
        Float16,
        Color,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public enum VertexFormat
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }
    
    public static VertexFormat ToVertexFormat(int format, UnityRevision version)
    {
        if (version.Major < 2019)
        {
            switch ((VertexFormat2017)format)
            {
                case VertexFormat2017.Float:
                    return VertexFormat.Float;
                case VertexFormat2017.Float16:
                    return VertexFormat.Float16;
                case VertexFormat2017.Color:
                case VertexFormat2017.UNorm8:
                    return VertexFormat.UNorm8;
                case VertexFormat2017.SNorm8:
                    return VertexFormat.SNorm8;
                case VertexFormat2017.UNorm16:
                    return VertexFormat.UNorm16;
                case VertexFormat2017.SNorm16:
                    return VertexFormat.SNorm16;
                case VertexFormat2017.UInt8:
                    return VertexFormat.UInt8;
                case VertexFormat2017.SInt8:
                    return VertexFormat.SInt8;
                case VertexFormat2017.UInt16:
                    return VertexFormat.UInt16;
                case VertexFormat2017.SInt16:
                    return VertexFormat.SInt16;
                case VertexFormat2017.UInt32:
                    return VertexFormat.UInt32;
                case VertexFormat2017.SInt32:
                    return VertexFormat.SInt32;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        else
        {
            return (VertexFormat)format;
        }
    }
    
    public static uint GetFormatSize(VertexFormat format)
    {
        switch (format)
        {
            case VertexFormat.Float:
            case VertexFormat.UInt32:
            case VertexFormat.SInt32:
                return 4u;
            case VertexFormat.Float16:
            case VertexFormat.UNorm16:
            case VertexFormat.SNorm16:
            case VertexFormat.UInt16:
            case VertexFormat.SInt16:
                return 2u;
            case VertexFormat.UNorm8:
            case VertexFormat.SNorm8:
            case VertexFormat.UInt8:
            case VertexFormat.SInt8:
                return 1u;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
    
    public static bool IsIntFormat(VertexFormat format)
    {
        return format >= VertexFormat.UInt8;
    }
    
    public static List<int> BytesToIntArray(byte[] inputBytes, VertexFormat format)
    {
        var size = GetFormatSize(format);
        var len = inputBytes.Length / size;
        var result = new int[len];
        for (int i = 0; i < len; i++)
        {
            switch (format)
            {
                case VertexFormat.UInt8:
                case VertexFormat.SInt8:
                    result[i] = inputBytes[i];
                    break;
                case VertexFormat.UInt16:
                case VertexFormat.SInt16:
                    result[i] = BinaryPrimitives.ReadInt16LittleEndian(inputBytes.AsSpan(i * 2));
                    break;
                case VertexFormat.UInt32:
                case VertexFormat.SInt32:
                    result[i] = BinaryPrimitives.ReadInt32LittleEndian(inputBytes.AsSpan(i * 4));
                    break;
            }
        }

        return result.ToList();
    }
    
    public static List<float> BytesToFloatArray(byte[] inputBytes, VertexFormat format)
    {
        var size = GetFormatSize(format);
        var len = inputBytes.Length / size;
        var result = new float[len];
        for (int i = 0; i < len; i++)
        {
            switch (format)
            {
                case VertexFormat.Float:
                    result[i] = BinaryPrimitives.ReadSingleLittleEndian(inputBytes.AsSpan(i * 4));
                    break;
                case VertexFormat.Float16:
                    //result[i] = Half.ToHalf((inputBytes, i * 2));
                    throw new NotImplementedException();
                    break;
                case VertexFormat.UNorm8:
                    result[i] = inputBytes[i] / 255f;
                    break;
                case VertexFormat.SNorm8:
                    result[i] = Math.Max((sbyte)inputBytes[i] / 127f, -1f);
                    break;
                case VertexFormat.UNorm16:
                    result[i] = BinaryPrimitives.ReadUInt16LittleEndian(inputBytes.AsSpan(i * 2)) / 65535f;
                    break;
                case VertexFormat.SNorm16:
                    result[i] = Math.Max(BinaryPrimitives.ReadInt16LittleEndian(inputBytes.AsSpan(i * 2)) / 32767f, -1f);
                    break;
            }
        }
        return result.ToList();
    }
    
    public class ProcessedMesh
    {
        public List<float> m_Vertices = [];
        public List<float> m_Normals = [];
        public List<float> m_Tangents = [];
        public List<float> m_Colors = [];
        public List<float> m_UV0 = [];
        public List<float> m_UV1 = [];
        public List<float> m_UV2 = [];
        public List<float> m_UV3 = [];
        public List<float> m_UV4 = [];
        public List<float> m_UV5 = [];
        public List<float> m_UV6 = [];
        public List<float> m_UV7 = [];
        public List<IBoneWeights4> m_Skin = [];
        
        public List<ushort> m_Indices = [];
    }
    
    public static ProcessedMesh GetProcessedMesh(AssetManager assetManager, IMesh mesh, Endianness endianness, int maxChannel = -1)
    {
        var version = assetManager.Version!;
        
        var processedMesh = new ProcessedMesh();
        
        var vertexData = mesh.m_VertexData;
        var streams = vertexData.GetStreams(version!);
        var vertexCount = vertexData.m_VertexCount;

        var channelCount = vertexData.m_Channels.Count;
        if (maxChannel >= 0 && maxChannel < channelCount)
            channelCount = maxChannel + 1;
        for (var chn = 0; chn < channelCount; chn++)
        {
            var channel = vertexData.m_Channels[chn];
            
            if (channel.dimension == 0)
                continue;
            
            var stream = streams[channel.stream];
            var channelMask = new BitArray([ (int)stream.channelMask ]);
            
            if (!channelMask.Get(chn))
                continue;
            
            var dimension = version.Major < 2018 && chn == 2 && channel.format == 2 //kShaderChannelColor && kChannelFormatColor
                ? 4 : channel.dimension;
            
            var vertexFormat = ToVertexFormat(channel.format, version);
            var componentByteSize = (int)GetFormatSize(vertexFormat);
            var componentBytes = new byte[vertexCount * dimension * componentByteSize];
            
            for (int v = 0; v < vertexCount; v++)
            {
                var vertexOffset = (int)stream.offset + channel.offset + (int)stream.stride * v;
                for (int d = 0; d < dimension; d++)
                {
                    var componentOffset = vertexOffset + componentByteSize * d;
                    Buffer.BlockCopy(vertexData.m_DataSize.data, componentOffset, componentBytes, componentByteSize * (v * dimension + d), componentByteSize);
                }
            }
            
            if (endianness == Endianness.BigEndian && componentByteSize > 1) //swap bytes
            {
                for (var i = 0; i < componentBytes.Length / componentByteSize; i++)
                {
                    var buff = new byte[componentByteSize];
                    Buffer.BlockCopy(componentBytes, i * componentByteSize, buff, 0, componentByteSize);
                    buff = buff.Reverse().ToArray();
                    Buffer.BlockCopy(buff, 0, componentBytes, i * componentByteSize, componentByteSize);
                }
            }
            
            List<int> componentsIntArray = [];
            List<float> componentsFloatArray = [];
            
            if (IsIntFormat(vertexFormat))
                componentsIntArray = BytesToIntArray(componentBytes, vertexFormat);
            else
                componentsFloatArray = BytesToFloatArray(componentBytes, vertexFormat);
            
            if (version.Major >= 2018)
            {
                switch (chn)
                {
                    case 0: //kShaderChannelVertex
                        processedMesh.m_Vertices = componentsFloatArray;
                        break;
                    case 1: //kShaderChannelNormal
                        processedMesh.m_Normals = componentsFloatArray;
                        break;
                    case 2: //kShaderChannelTangent
                        processedMesh.m_Tangents = componentsFloatArray;
                        break;
                    case 3: //kShaderChannelColor
                        processedMesh.m_Colors = componentsFloatArray;
                        break;
                    case 4: //kShaderChannelTexCoord0
                        processedMesh.m_UV0 = componentsFloatArray;
                        break;
                    case 5: //kShaderChannelTexCoord1
                        processedMesh.m_UV1 = componentsFloatArray;
                        break;
                    case 6: //kShaderChannelTexCoord2
                        processedMesh.m_UV2 = componentsFloatArray;
                        break;
                    case 7: //kShaderChannelTexCoord3
                        processedMesh.m_UV3 = componentsFloatArray;
                        break;
                    case 8: //kShaderChannelTexCoord4
                        processedMesh.m_UV4 = componentsFloatArray;
                        break;
                    case 9: //kShaderChannelTexCoord5
                        processedMesh.m_UV5 = componentsFloatArray;
                        break;
                    case 10: //kShaderChannelTexCoord6
                        processedMesh.m_UV6 = componentsFloatArray;
                        break;
                    case 11: //kShaderChannelTexCoord7
                        processedMesh.m_UV7 = componentsFloatArray;
                        break;
                    //2018.2 and up
                    case 12: //kShaderChannelBlendWeight
                        /*if (m_Skin == null)
                        {
                            InitMSkin();
                        }
                        for (int i = 0; i < m_VertexCount; i++)
                        {
                            for (int j = 0; j < m_Channel.dimension; j++)
                            {
                                m_Skin[i].weight[j] = componentsFloatArray[i * m_Channel.dimension + j];
                            }
                        }*/
                        break;
                    case 13: //kShaderChannelBlendIndices
                        /*if (m_Skin == null)
                        {
                            InitMSkin();
                            if (m_Channel.dimension == 1)
                            {
                                for (var i = 0; i < m_VertexCount; i++)
                                {
                                    m_Skin[i].weight[0] = 1f;
                                }
                            }
                        }
                        for (int i = 0; i < m_VertexCount; i++)
                        {
                            for (int j = 0; j < m_Channel.dimension; j++)
                            {
                                m_Skin[i].boneIndex[j] = componentsIntArray[i * m_Channel.dimension + j];
                            }
                        }*/
                        break;
                }
            }
            else
            {
                switch (chn)
                {
                    case 0: //kShaderChannelVertex
                        processedMesh.m_Vertices = componentsFloatArray;
                        break;
                    case 1: //kShaderChannelNormal
                        processedMesh.m_Normals = componentsFloatArray;
                        break;
                    case 2: //kShaderChannelColor
                        processedMesh.m_Colors = componentsFloatArray;
                        break;
                    case 3: //kShaderChannelTexCoord0
                        processedMesh.m_UV0 = componentsFloatArray;
                        break;
                    case 4: //kShaderChannelTexCoord1
                        processedMesh.m_UV1 = componentsFloatArray;
                        break;
                    case 5:
                        if (version.Major >= 5) //kShaderChannelTexCoord2
                        {
                            processedMesh.m_UV2 = componentsFloatArray;
                        }
                        else //kShaderChannelTangent
                        {
                            processedMesh.m_Tangents = componentsFloatArray;
                        }
                        break;
                    case 6: //kShaderChannelTexCoord3
                        processedMesh.m_UV3 = componentsFloatArray;
                        break;
                    case 7: //kShaderChannelTangent
                        processedMesh.m_Tangents = componentsFloatArray;
                        break;
                }
            }
        }
        
        var m_Use16BitIndices = false;
        //Unity fixed it in 2017.3.1p1 and later versions
        if (version  >= "2017.4" || //2017.4
            (version == "2017.3.1" && version.Extra.StartsWith('p')) || //fixed after 2017.3.1px
            (version.Major == 2017 && version.Minor == 3 && mesh.m_MeshCompression == 0))//2017.3.xfx with no compression
        {
            m_Use16BitIndices = mesh.m_IndexFormat == 0;
        }
        
        if (!m_Use16BitIndices)
            throw new Exception("Failed to extract mesh data: uint32 indices are not supported.");

        var m_IndexBuffer = mesh.m_IndexBuffer;
        var m_IndexBuffer_size = m_IndexBuffer.Count;
        var indexBuffer = m_Use16BitIndices
            ? Enumerable.Range(0, m_IndexBuffer_size / 2)
                .Select(i =>
                {
                    int byteIndex = i * 2;
                    return endianness == Endianness.BigEndian
                        ? (ushort)((m_IndexBuffer[byteIndex] << 8) | m_IndexBuffer[byteIndex + 1])
                        : (ushort)((m_IndexBuffer[byteIndex + 1] << 8) | m_IndexBuffer[byteIndex]);
                })
                .ToArray()
            : throw new Exception("Failed to extract mesh data: uint32 indices are not supported.");

        var indices = processedMesh.m_Indices;

        foreach (var m_SubMesh in mesh.m_SubMeshes)
        {
            var firstIndex = (int)(m_SubMesh.firstByte / 2);
            if (!m_Use16BitIndices)
            {
                firstIndex /= 2;
            }
            var indexCount = m_SubMesh.indexCount;
            var topology = m_SubMesh.topology;
            if (topology == (int)GfxPrimitiveType.Triangles)
            {
                for (int i = 0; i < indexCount; i += 3)
                {
                    indices.Add(indexBuffer[firstIndex + i]);
                    indices.Add(indexBuffer[firstIndex + i + 1]);
                    indices.Add(indexBuffer[firstIndex + i + 2]);
                }
            }
            else if (topology == (int)GfxPrimitiveType.Quads) //Quads
            {
                throw new NotSupportedException("Failed getting triangles. Submesh topology is quads.");
                /*for (int q = 0; q < indexCount; q += 4)
                {
                    m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 1]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 2]);
                    m_Indices.Add(m_IndexBuffer[firstIndex + q + 3]);
                }
                //fix indexCount
                m_SubMesh.indexCount = indexCount / 2 * 3;*/
            }
            else
            {
                throw new NotSupportedException("Failed getting triangles. Submesh topology is lines or points.");
            }
        }
        
        return processedMesh;
    }
}