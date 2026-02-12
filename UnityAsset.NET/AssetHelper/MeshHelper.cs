// based on https://github.com/RazTools/Studio/blob/main/AssetStudio/Classes/Mesh.cs#L478
using System.Buffers.Binary;
using System.Collections;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;
using UnityAsset.NET.TypeTree.PreDefined.Types;
using Half = UnityAsset.NET.Extensions.Half;

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
                    result[i] = Half.ToHalf(inputBytes, i * 2);
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

    public class ProcessedSubMesh
    {
        public List<ushort> m_Indices = [];
        public int m_IndexCount = 0;
    }
    
    public class ProcessedMesh
    {
        public int m_VertexCount = 0;
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
        public List<BoneWeights4> m_Skin = [];
        
        public List<ProcessedSubMesh> m_SubMeshes = [];
        
        public List<float> GetUV(int uv)
        {
            switch (uv)
            {
                case 0:
                    return m_UV0;
                case 1:
                    return m_UV1;
                case 2:
                    return m_UV2;
                case 3:
                    return m_UV3;
                case 4:
                    return m_UV4;
                case 5:
                    return m_UV5;
                case 6:
                    return m_UV6;
                case 7:
                    return m_UV7;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public void SetUV(int uv, List<float> m_UV)
        {
            switch (uv)
            {
                case 0:
                    m_UV0 = m_UV;
                    break;
                case 1:
                    m_UV1 = m_UV;
                    break;
                case 2:
                    m_UV2 = m_UV;
                    break;
                case 3:
                    m_UV3 = m_UV;
                    break;
                case 4:
                    m_UV4 = m_UV;
                    break;
                case 5:
                    m_UV5 = m_UV;
                    break;
                case 6:
                    m_UV6 = m_UV;
                    break;
                case 7:
                    m_UV7 = m_UV;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    public static ProcessedMesh GetProcessedMesh(AssetManager assetManager, IMesh mesh, Endianness endianness)
    {
        var version = assetManager.Version!;
        
        var processedMesh = new ProcessedMesh();
        
        // ReadVertexData
        var vertexData = mesh.m_VertexData;
        var streams = vertexData.GetStreams(version!);
        var vertexCount = vertexData.m_VertexCount;
        processedMesh.m_VertexCount = (int)vertexCount;
        
        for (var chn = 0; chn < vertexData.m_Channels.Length; chn++)
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
                        if (processedMesh.m_Skin.Count == 0)
                            processedMesh.m_Skin = Enumerable.Repeat(new BoneWeights4(), (int)vertexCount).ToList();
                        for (int i = 0; i < vertexCount; i++)
                        {
                            for (int j = 0; j < dimension; j++)
                            {
                                processedMesh.m_Skin[i].weight[j] = componentsFloatArray[i * dimension + j];
                            }
                        }
                        break;
                    case 13: //kShaderChannelBlendIndices
                        if (processedMesh.m_Skin.Count == 0)
                        {
                            processedMesh.m_Skin = Enumerable.Repeat(new BoneWeights4(), (int)vertexCount).ToList();
                            if (dimension == 1)
                            {
                                for (var i = 0; i < vertexCount; i++)
                                {
                                    processedMesh.m_Skin[i].weight[0] = 1f;
                                }
                            }
                        }
                        for (int i = 0; i < vertexCount; i++)
                        {
                            for (int j = 0; j < dimension; j++)
                            {
                                processedMesh.m_Skin[i].boneIndex[j] = componentsIntArray[i * dimension + j];
                            }
                        }
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
                    case 5: //kShaderChannelTexCoord2
                        processedMesh.m_UV2 = componentsFloatArray;
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
        var m_IndexBuffer_size = m_IndexBuffer.Length;
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
        
        // DecompressCompressedMesh
        
        //Vertex
        var compressedMesh = mesh.m_CompressedMesh;
        if (compressedMesh.m_Vertices.m_NumItems > 0)
        {
            processedMesh.m_VertexCount = (int)(compressedMesh.m_Vertices.m_NumItems / 3);
            processedMesh.m_Vertices = compressedMesh.m_Vertices.UnpackFloats(3, 3 * 4);
        }
        //UV
        if (compressedMesh.m_UV.m_NumItems > 0)
        {
            var m_UVInfo = compressedMesh.m_UVInfo;
            if (m_UVInfo != 0)
            {
                const int kInfoBitsPerUV = 4;
                const int kUVDimensionMask = 3;
                const int kUVChannelExists = 4;
                const int kMaxTexCoordShaderChannels = 8;

                int uvSrcOffset = 0;
                for (int uv = 0; uv < kMaxTexCoordShaderChannels; uv++)
                {
                    var texCoordBits = m_UVInfo >> (uv * kInfoBitsPerUV);
                    texCoordBits &= (1u << kInfoBitsPerUV) - 1u;
                    if ((texCoordBits & kUVChannelExists) != 0)
                    {
                        var uvDim = 1 + (int)(texCoordBits & kUVDimensionMask);
                        var m_UV = compressedMesh.m_UV.UnpackFloats(uvDim, uvDim * 4, uvSrcOffset, processedMesh.m_VertexCount);
                        processedMesh.SetUV(uv, m_UV);
                        uvSrcOffset += uvDim * processedMesh.m_VertexCount;
                    }
                }
            }
            else
            {
                processedMesh.m_UV0 = compressedMesh.m_UV.UnpackFloats(2, 2 * 4, 0, processedMesh.m_VertexCount);
                if (compressedMesh.m_UV.m_NumItems >= processedMesh.m_VertexCount * 4)
                {
                    processedMesh.m_UV1 = compressedMesh.m_UV.UnpackFloats(2, 2 * 4, processedMesh.m_VertexCount * 2, processedMesh.m_VertexCount);
                }
            }
        }
        //Normal
        if (compressedMesh.m_Normals.m_NumItems > 0)
        {
            var normalData = compressedMesh.m_Normals.UnpackFloats(2, 4 * 2);
            var signs = compressedMesh.m_NormalSigns.UnpackInts();
            var m_Normals = new float[compressedMesh.m_Normals.m_NumItems / 2 * 3];
            for (int i = 0; i < compressedMesh.m_Normals.m_NumItems / 2; ++i)
            {
                var x = normalData[i * 2 + 0];
                var y = normalData[i * 2 + 1];
                var zsqr = 1 - x * x - y * y;
                float z;
                if (zsqr >= 0f)
                    z = (float)Math.Sqrt(zsqr);
                else
                {
                    z = 0;
                    (x, y, z) = (x, y, z).Normalize();
                }
                if (signs[i] == 0)
                    z = -z;
                m_Normals[i * 3] = x;
                m_Normals[i * 3 + 1] = y;
                m_Normals[i * 3 + 2] = z;
            }
            processedMesh.m_Normals = new(m_Normals);
        }
        //Tangent
        if (compressedMesh.m_Tangents.m_NumItems > 0)
        {
            var tangentData = compressedMesh.m_Tangents.UnpackFloats(2, 4 * 2);
            var signs = compressedMesh.m_TangentSigns.UnpackInts();
            var m_Tangents = new float[compressedMesh.m_Tangents.m_NumItems / 2 * 4];
            for (int i = 0; i < compressedMesh.m_Tangents.m_NumItems / 2; ++i)
            {
                var x = tangentData[i * 2 + 0];
                var y = tangentData[i * 2 + 1];
                var zsqr = 1 - x * x - y * y;
                float z;
                if (zsqr >= 0f)
                    z = (float)Math.Sqrt(zsqr);
                else
                {
                    z = 0;
                    (x, y, z) = (x, y, z).Normalize();
                }
                if (signs[i * 2 + 0] == 0)
                    z = -z;
                var w = signs[i * 2 + 1] > 0 ? 1.0f : -1.0f;
                m_Tangents[i * 4] = x;
                m_Tangents[i * 4 + 1] = y;
                m_Tangents[i * 4 + 2] = z;
                m_Tangents[i * 4 + 3] = w;
            }
            processedMesh.m_Tangents = new(m_Tangents);
        }
        //FloatColor
        if (compressedMesh.m_FloatColors.m_NumItems > 0)
        {
            processedMesh.m_Colors = compressedMesh.m_FloatColors.UnpackFloats(1, 4);
        }
        //Skin
        if (compressedMesh.m_Weights.m_NumItems > 0)
        {
            var weights = compressedMesh.m_Weights.UnpackInts();
            var boneIndices = compressedMesh.m_BoneIndices.UnpackInts();

            var m_Skin = new BoneWeights4[processedMesh.m_VertexCount];

            int bonePos = 0;
            int boneIndexPos = 0;
            int j = 0;
            int sum = 0;

            for (int i = 0; i < compressedMesh.m_Weights.m_NumItems; i++)
            {
                //read bone index and weight.
                m_Skin[bonePos].weight[j] = weights[i] / 31.0f;
                m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                j++;
                sum += weights[i];

                //the weights add up to one. fill the rest for this vertex with zero, and continue with next one.
                if (sum >= 31)
                {
                    for (; j < 4; j++)
                    {
                        m_Skin[bonePos].weight[j] = 0;
                        m_Skin[bonePos].boneIndex[j] = 0;
                    }
                    bonePos++;
                    j = 0;
                    sum = 0;
                }
                //we read three weights, but they don't add up to one. calculate the fourth one, and read
                //missing bone index. continue with next vertex.
                else if (j == 3)
                {
                    m_Skin[bonePos].weight[j] = (31 - sum) / 31.0f;
                    m_Skin[bonePos].boneIndex[j] = boneIndices[boneIndexPos++];
                    bonePos++;
                    j = 0;
                    sum = 0;
                }
            }
            processedMesh.m_Skin = new(m_Skin);
        }
        //IndexBuffer
        if (compressedMesh.m_Triangles.m_NumItems > 0)
        {
            indexBuffer = compressedMesh.m_Triangles.UnpackInts().Select(x => checked((ushort)x)).ToArray();
        }
        
        // GetTriangles

        foreach (var m_SubMesh in mesh.m_SubMeshes)
        {
            var subMesh = new ProcessedSubMesh();
            var indices = subMesh.m_Indices;
            var firstIndex = (int)(m_SubMesh.firstByte / 2);
            if (!m_Use16BitIndices)
            {
                firstIndex /= 2;
            }
            var indexCount = m_SubMesh.indexCount;
            subMesh.m_IndexCount = (int)indexCount;
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
            
            processedMesh.m_SubMeshes.Add(subMesh);
        }
        
        return processedMesh;
    }

    public static void ExportToObj(ProcessedMesh mesh, string dir, string name)
    {
        if (mesh.m_Vertices.Count == 0 || mesh.m_SubMeshes.Count == 0)
            throw new Exception("Mesh is missing required data.");
        var objPath = Path.Combine(dir, name + ".obj");
        using var writer = new StreamWriter(objPath);
        writer.WriteLine($"g {name}");
        var vertices = mesh.m_Vertices;
        var step = vertices.Count == mesh.m_VertexCount * 3 ? 3 : 4;
        for (int i = 0; i < vertices.Count; i += step)
        {
            writer.WriteLine($"v {-vertices[i]} {vertices[i + 1]} {vertices[i + 2]}");
        }
        var uv0 = mesh.m_UV0;
        step = uv0.Count == mesh.m_VertexCount * 2 
            ? 2 
            : uv0.Count == mesh.m_VertexCount * 3 
                ? 3 
                : 4;
        if (uv0.Count > 0)
        {
            for (int i = 0; i < uv0.Count; i += step)
            {
                writer.WriteLine($"vt {uv0[i]} {uv0[i + 1]}");
            }
        }
        var normals = mesh.m_Normals;
        step = normals.Count == mesh.m_VertexCount * 3 ? 3 : 4;
        if (normals.Count > 0)
        {
            for (int i = 0; i < normals.Count; i += step)
            {
                writer.WriteLine($"vn {-normals[i]} {normals[i + 1]} {normals[i + 2]}");
            }
        }
        int subMeshIndex = 0;
        foreach (var subMesh in mesh.m_SubMeshes)
        {
            writer.WriteLine($"g {name}_{subMeshIndex}");
            var indices = subMesh.m_Indices;
            for (int i = 0; i < indices.Count; i += 3)
            {
                int v1 = indices[i + 2] + 1;
                int v2 = indices[i + 1] + 1;
                int v3 = indices[i + 0] + 1;
        
                // f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                if (uv0.Count > 0 && normals.Count > 0)
                {
                    writer.WriteLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                }
                else if (normals.Count > 0)
                {
                    writer.WriteLine($"f {v1}//{v1} {v2}//{v2} {v3}//{v3}");
                }
                else
                {
                    writer.WriteLine($"f {v1} {v2} {v3}");
                }
            }
        }
    }
}