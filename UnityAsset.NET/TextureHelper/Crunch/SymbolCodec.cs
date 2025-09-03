using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    internal sealed class SymbolCodec
    {
        public byte[] DecodeBuf = [];
        public UInt32 DecodeBufNext;
        public UInt32 DecodeBufEnd;
        
        public UInt32 BitBuf;
        public Int32 BitCount;
        
        public void StartDecoding(byte[] buf, UInt32 offset, UInt32 size)
        {
            if (buf.Length == 0)
                throw new Exception("buffer length is zero in StartDecoding");

            DecodeBuf = buf;
            DecodeBufNext = offset;
            DecodeBufEnd = offset + size;

            BitBuf = 0;
            BitCount = 0;
        }

        public void DecodeReceiveStaticDataModel(StaticHuffmanDataModel model)
        {
            UInt32 totalUsedSyms = DecodeBits(TotalBits(MAX_SUPPORTED_SYMS));

            if (totalUsedSyms == 0)
                throw new Exception("totalUsedSyms is zero in DecodeReceiveStaticDataModel");
            
            model.CodeSizes = new byte[totalUsedSyms];
            
            UInt32 numCodelengthCodesToSend = DecodeBits(5);
            if (numCodelengthCodesToSend < 1 || numCodelengthCodesToSend > MAX_CODELENGTH_CODES)
                throw new Exception("numCodelengthCodesToSend out of range in DecodeReceiveStaticDataModel");

            var dm = new StaticHuffmanDataModel();
            dm.CodeSizes = new byte[MAX_CODELENGTH_CODES];

            for (int i = 0; i < numCodelengthCodesToSend; i++)
            {
                dm.CodeSizes[MOST_PROBABLE_CODELENGTH_CODES[i]] = (byte)DecodeBits(3);
            }
            
            dm.PrepareDecoderTables();

            UInt32 ofs = 0;
            while (ofs < totalUsedSyms)
            {
                UInt32 numRemaining = totalUsedSyms - ofs;

                UInt32 code = Decode(dm);
                if (code <= 16)
                    model.CodeSizes[ofs++] = (byte)code;
                else if (code == SMALL_ZERO_RUN_CODE)
                {
                    UInt32 len = DecodeBits(SMALL_ZERO_RUN_EXTRA_BITS) + MIN_SMALL_ZERO_RUN_SIZE;
                    if (len > numRemaining)
                        throw new Exception("len is greater than the remaining length");
                    ofs += len;
                }
                else if (code == LARGE_ZERO_RUN_CODE)
                {
                    UInt32 len = DecodeBits(LARGE_ZERO_RUN_EXTRA_BITS) + MIN_LARGE_ZERO_RUN_SIZE;
                    if (len > numRemaining)
                        throw new Exception("len is greater than the remaining length");
                    ofs += len;
                }
                else if (code == SMALL_REPEAT_CODE || code == LARGE_REPEAT_CODE)
                {
                    UInt32 len =
                        code == SMALL_REPEAT_CODE
                            ? DecodeBits(SMALL_NON_ZERO_RUN_EXTRA_BITS) + SMALL_MIN_NON_ZERO_RUN_SIZE
                            : DecodeBits(LARGE_NON_ZERO_RUN_EXTRA_BITS) + LARGE_MIN_NON_ZERO_RUN_SIZE;
                    
                    if (ofs == 0 || len > numRemaining)
                        throw new Exception("len is greater than the remaining length or ofs is zero");
                    UInt32 prev = model.CodeSizes[ofs - 1];
                    if (prev == 0)
                        throw new Exception("previous code size is zero in repeat");
                    UInt32 end = ofs + len;
                    while (ofs < end)
                        model.CodeSizes[ofs++] = (byte)prev;
                }
                else
                {
                    throw new Exception("invalid code in DecodeReceiveStaticDataModel");
                }
            }
            
            if (ofs != totalUsedSyms)
                throw new Exception("ofs does not equal totalUsedSyms in DecodeReceiveStaticDataModel");
            
            model.PrepareDecoderTables();
        }

        public UInt32 Decode(StaticHuffmanDataModel model)
        {
            if (model.DecoderTables == null)
                throw new Exception("DecoderTables is null in Decode");
            DecoderTables tables = model.DecoderTables;

            if (BitCount < 24)
            {
                if (BitCount < 16)
                {
                    UInt32 c0 = 0;
                    UInt32 c1 = 0;
                    if (DecodeBufNext < DecodeBufEnd)
                        c0 = DecodeBuf[DecodeBufNext++];
                    if (DecodeBufNext < DecodeBufEnd)
                        c1 = DecodeBuf[DecodeBufNext++];
                    BitCount += 16;
                    UInt32 c = (c0 << 8) | c1;
                    BitBuf |= c << (32 - BitCount);
                }
                else
                {
                    UInt32 c = DecodeBufNext < DecodeBufEnd ? (UInt32)DecodeBuf[DecodeBufNext++] : 0;
                    BitCount += 8;
                    BitBuf |= c << (32 - BitCount);
                }
            }
            
            UInt32 k = (BitBuf >> 16) + 1;
            UInt32 sym;
            UInt32 len;

            if (k <= tables.TableMaxCode)
            {
                UInt32 t = tables.Lookup[BitBuf >> (int)(32 - tables.TableBits)];
                
                Debug.Assert(t != UInt32.MaxValue);
                sym = t & UInt16.MaxValue;
                len = t >> 16;
                
                Debug.Assert(model.CodeSizes[sym] == len);
            }
            else
            {
                len = tables.DecodeStartCodeSize;

                while (k > tables.MaxCodes[len - 1])
                {
                    len++;
                }

                var valPtr = tables.ValPtrs[len - 1] + (int)(BitBuf >> (int)(32 - len));
                
                if (valPtr >= model.TotalSyms)
                    throw new Exception("valPtr out of range in Decode");
                
                sym = tables.SortedSymbolOrder[valPtr];
            }

            BitBuf <<= (int)len;
            BitCount -= (int)len;

            return sym;
        }

        public UInt32 DecodeBits(UInt32 numBits)
        {
            if (numBits == 0)
                return 0;
            if (numBits > 16)
            {
                UInt32 a = GetBits(numBits - 16);
                UInt32 b = GetBits(16);
                
                return (a << 16) | b;
            }
            return GetBits(numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt32 GetBits(UInt32 numBits)
        {
            Debug.Assert(numBits <= 32);

            while (BitCount < numBits)
            {
                UInt32 c = 0;
                if (DecodeBufNext < DecodeBufEnd)
                    c = DecodeBuf[DecodeBufNext++];
                
                BitCount += 8;
                Debug.Assert(BitCount <= BIT_BUF_SIZE);
                BitBuf |= c << (int)(BIT_BUF_SIZE - BitCount);
            }
            
            UInt32 result = BitBuf >> (int)(BIT_BUF_SIZE - numBits);
            BitBuf <<= (int)numBits;
            BitCount -= (int)numBits;
            
            return result;
        }
    }
}