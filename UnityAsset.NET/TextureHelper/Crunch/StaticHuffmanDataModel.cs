using System.Diagnostics;

namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    internal sealed class StaticHuffmanDataModel
    {
        public UInt32 TotalSyms = 0;
        public byte[] CodeSizes = [];
        public DecoderTables? DecoderTables;

        /*public StaticHuffmanDataModel(UInt32 totalSyms, byte[] codeSizes, UInt32 codeSizeLimit)
        {
            Debug.Assert(totalSyms >= 1 && totalSyms <= MAX_SUPPORTED_SYMS && codeSizeLimit >= 1);

            codeSizeLimit = Math.Min(codeSizeLimit, MAX_EXPECTED_CODE_SIZE);
            
            CodeSizes = new byte[totalSyms];
            
            UInt32 minCodeSize = UInt32.MaxValue;
            UInt32 maxCodeSize = 0;
            
            
            Array.Copy(codeSizes, CodeSizes, totalSyms);
            for (UInt32 i = 0; i < totalSyms; i++)
            {
                var s = codeSizes[i];
                minCodeSize = Math.Min(minCodeSize, s);
                maxCodeSize = Math.Max(maxCodeSize, s);
            }
            
            if ((maxCodeSize < 1) || (maxCodeSize > 32) || (minCodeSize > codeSizeLimit))
                throw new Exception("Invalid code sizes in StaticHuffmanDataModel");
            
            if (maxCodeSize > codeSizeLimit)
                throw new Exception("maxCodeSize exceeds codeSizeLimit in StaticHuffmanDataModel");
            
            DecoderTables = new DecoderTables(totalSyms, CodeSizes, ComputeDecoderTableBits());
        }*/

        public void PrepareDecoderTables()
        {
            UInt32 totalSyms = (UInt32)CodeSizes.Length;
            
            Debug.Assert(totalSyms >= 1 && totalSyms <= MAX_SUPPORTED_SYMS);
            
            TotalSyms = totalSyms;
            
            DecoderTables ??= new DecoderTables(TotalSyms, CodeSizes, ComputeDecoderTableBits());
        }

        private UInt32 ComputeDecoderTableBits()
        {
            UInt32 decoderTableBits = 0;
            if (TotalSyms > 16)
                decoderTableBits = Math.Min(1 + CeilLog2i(TotalSyms), MAX_TABLE_BITS);
            return decoderTableBits;
        }
    }
}