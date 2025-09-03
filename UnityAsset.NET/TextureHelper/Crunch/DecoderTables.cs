using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    internal sealed class DecoderTables
    {
        public UInt32 NumSyms;
        public UInt32 TotalUsedSyms;
        public UInt32 TableBits;
        public UInt32 TableShift;
        public UInt32 TableMaxCode;
        public UInt32 DecodeStartCodeSize;
        
        public byte MinCodeSize;
        public byte MaxCodeSize;

        public UInt32[] MaxCodes;
        public Int32[] ValPtrs;
        
        public UInt32[] Lookup;
        public UInt16[] SortedSymbolOrder;

        public DecoderTables(UInt32 numSyms, byte[] codeSizes, UInt32 tableBits)
        {
            Span<UInt32> minCodes = stackalloc UInt32[(int)MAX_EXPECTED_CODE_SIZE];
            if (tableBits > MAX_TABLE_BITS)
                throw new Exception("tableBits exceeds MAX_TABLE_BITS");
            
            NumSyms = numSyms;
            
            MaxCodes = new UInt32[MAX_EXPECTED_CODE_SIZE + 1];
            ValPtrs = new Int32[MAX_EXPECTED_CODE_SIZE + 1];
            
            Span<UInt32> numCodes = stackalloc UInt32[(int)MAX_EXPECTED_CODE_SIZE + 1];
            for (int i = 0; i < numSyms; i++)
            {
                var c = codeSizes[i];
                if (c != 0)
                {
                    numCodes[c]++;
                }
            }
            
            Span<UInt32> sortedPositions = stackalloc UInt32[(int)MAX_EXPECTED_CODE_SIZE + 1];
            
            UInt32 curCode = 0;
            
            UInt32 totalUsedSyms = 0;
            UInt32 maxCodeSize = 0;
            UInt32 minCodeSize = UInt32.MaxValue;
            for (UInt32 i = 1; i <= MAX_EXPECTED_CODE_SIZE; i++)
            {
                UInt32 n = numCodes[(int)i];

                if (n == 0)
                    MaxCodes[i - 1] = 0;
                else
                {
                    minCodeSize = Math.Min(minCodeSize, i);
                    maxCodeSize = Math.Max(maxCodeSize, i);
                    
                    minCodes[(int)(i - 1)] = curCode;

                    MaxCodes[(int)(i - 1)] = curCode + n - 1;
                    MaxCodes[(int)(i - 1)] = 1 + ((MaxCodes[(int)(i - 1)] << (int)(16 - i)) | (UInt32)((1 << (int)(16 - i)) - 1));
                    
                    ValPtrs[(int)(i - 1)] = (int)totalUsedSyms;
                    
                    sortedPositions[(int)i] = totalUsedSyms;

                    curCode += n;
                    totalUsedSyms += n;
                }
                
                curCode <<= 1;
            }
            
            TotalUsedSyms = totalUsedSyms;
            
            var curSortedSymbolOrderSize = totalUsedSyms;
            
            if (!IsPowerOf2(totalUsedSyms))
                curSortedSymbolOrderSize = Math.Min(numSyms, NextPower2(totalUsedSyms));
            
            SortedSymbolOrder = new UInt16[curSortedSymbolOrderSize];

            unchecked
            {
                MinCodeSize = (byte)minCodeSize;
                MaxCodeSize = (byte)maxCodeSize;
            }

            for (int i = 0; i < numSyms; i++)
            {
                var c = codeSizes[i];
                if (c != 0)
                {
                    Debug.Assert(numCodes[c] != 0);
                    
                    var sortedPos = sortedPositions[c]++;
                    
                    Debug.Assert(sortedPos < totalUsedSyms);
                    
                    SortedSymbolOrder[(int)sortedPos] = (UInt16)i;
                }
            }

            if (tableBits <= MinCodeSize)
                tableBits = 0;
            TableBits = tableBits;

            if (tableBits != 0)
            {
                var tableSize = 1 << (int)tableBits;

                Lookup = new UInt32[tableSize];
                for (int i = 0; i < tableSize; i++)
                {
                    Lookup[i] = UInt32.MaxValue;
                }

                for (UInt32 codeSize = 1; codeSize <= tableBits; codeSize++)
                {
                    if (numCodes[(int)codeSize] == 0)
                        continue;
                    
                    UInt32 fillszie = tableBits - codeSize;
                    UInt32 fillnum = (UInt32)(1 << (int)fillszie);
                    
                    UInt32 minCode = minCodes[(int)(codeSize - 1)];
                    UInt32 maxCode = GetUnshiftedMaxCode(codeSize);
                    UInt32 valPtr = (UInt32)ValPtrs[(int)(codeSize - 1)];
                    
                    for (UInt32 code = minCode; code <= maxCode; code++)
                    {
                        UInt32 symIndex = SortedSymbolOrder[(int)(valPtr + code - minCode)];
                        Debug.Assert(codeSizes[(int)symIndex] == codeSize);
                        
                        for (UInt32 j = 0; j < fillnum; j++)
                        {
                            UInt32 t = j + (code << (int)fillszie);
                            Debug.Assert(t < (1 << (int)tableBits));
                            Debug.Assert(Lookup[(int)t] == UInt32.MaxValue);
                            Lookup[(int)t] = symIndex | (codeSize << 16);
                        }
                    }
                }
            }
            else
            {
                Lookup = [];
            }

            for (int i = 0; i < MAX_EXPECTED_CODE_SIZE; i++)
                ValPtrs[i] -= (Int32)minCodes[i];
            
            TableMaxCode = 0;
            DecodeStartCodeSize = MinCodeSize;

            if (tableBits != 0)
            {
                UInt32 i;
                for (i = tableBits; i >= 1; i--)
                {
                    if (numCodes[(int)i] != 0)
                    {
                        TableMaxCode = MaxCodes[(int)(i - 1)];
                        break;
                    }
                }

                if (i >= 1)
                {
                    DecodeStartCodeSize = tableBits + 1;
                    for (UInt32 j = tableBits + 1; j <= MAX_EXPECTED_CODE_SIZE; j++)
                    {
                        if (numCodes[(int)j] != 0)
                        {
                            DecodeStartCodeSize = j;
                            break;
                        }
                    }
                }
            }
            
            // sentinels
            MaxCodes[MAX_EXPECTED_CODE_SIZE] = UInt32.MaxValue;
            ValPtrs[MAX_EXPECTED_CODE_SIZE] = 0xFFFFF;
            
            TableShift = 32 - TableBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UInt32 GetUnshiftedMaxCode(UInt32 len)
        {
            Debug.Assert(len >= 1 && len <= MAX_EXPECTED_CODE_SIZE);
            UInt32 k = MaxCodes[(int)(len - 1)];
            if (k == 0)
                return UInt32.MaxValue;
            return (k - 1) >> (int)(16 - len);
        }
    }
}