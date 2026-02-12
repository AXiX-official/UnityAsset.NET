using System.Diagnostics;

namespace UnityAsset.NET.TypeTree.PreDefined.Interfaces;

public partial interface IPackedBitVector
{
    public List<int> UnpackInts()
    {
        Debug.Assert(m_BitSize is not null);
        var data = new List<int>((int)m_NumItems);
        int indexPos = 0;
        int bitPos = 0;
        for (int i = 0; i < m_NumItems; i++)
        {
            int bits = 0;
            data.Add(0);
            while (bits < m_BitSize.Value)
            {
                data[i] |= (m_Data[indexPos] >> bitPos) << bits;
                int num = Math.Min(m_BitSize.Value - bits, 8 - bitPos);
                bitPos += num;
                bits += num;
                if (bitPos == 8)
                {
                    indexPos++;
                    bitPos = 0;
                }
            }
            data[i] &= (1 << m_BitSize.Value) - 1;
        }
        return data;
    }
    
    public List<float> UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
    {
        Debug.Assert(m_BitSize is not null);
        Debug.Assert(m_Range is not null);
        Debug.Assert(m_Start is not null);
        int bitPos = m_BitSize.Value * start;
        int indexPos = bitPos / 8;
        bitPos %= 8;

        float scale = 1.0f / m_Range.Value;
        if (numChunks == -1)
            numChunks = (int)m_NumItems / itemCountInChunk;
        var end = chunkStride * numChunks / 4;
        var data = new List<float>();
        for (var index = 0; index != end; index += chunkStride / 4)
        {
            for (int i = 0; i < itemCountInChunk; ++i)
            {
                uint x = 0;

                int bits = 0;
                while (bits < m_BitSize.Value)
                {
                    x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                    int num = Math.Min(m_BitSize.Value - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }
                x &= (uint)(1 << m_BitSize.Value) - 1u;
                data.Add(x / (scale * ((1 << m_BitSize.Value) - 1)) + m_Start.Value);
            }
        }
        return data;
    }
}