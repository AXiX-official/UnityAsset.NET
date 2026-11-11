using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace UnityAsset.NET.AssetHelper.TextureHelper.Crunch;

public static partial class Crunch
{
    private sealed class StaticHuffmanDataModel : IDisposable
    {
        public UInt32 TotalSyms;
        public byte[] CodeSizes = [];
        public int CodeSizesLength;
        public DecoderTables? DecoderTables;

        public void Resize(UInt32 len)
        {
            if (CodeSizes.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(CodeSizes);
            }

            if (len > 0)
            {
                CodeSizes = ArrayPool<byte>.Shared.Rent((int)len);
                CodeSizes.AsSpan(0, (int)len).Clear();
            }
            else
            {
                CodeSizes = [];
            }
            CodeSizesLength = (int)len;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareDecoderTables()
        {
            UInt32 totalSyms = (UInt32)CodeSizesLength;
            
            Debug.Assert(totalSyms >= 1 && totalSyms <= MaxSupportedSyms);
            
            TotalSyms = totalSyms;
            
            DecoderTables ??= new DecoderTables(TotalSyms, CodeSizes.AsSpan(0, CodeSizesLength), ComputeDecoderTableBits());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UInt32 ComputeDecoderTableBits()
        {
            UInt32 decoderTableBits = 0;
            if (TotalSyms > 16)
                decoderTableBits = Math.Min(1 + CeilLog2I(TotalSyms), MaxTableBits);
            return decoderTableBits;
        }
        
        public void Dispose()
        {
            if (CodeSizes.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(CodeSizes);
                CodeSizes = [];
            }
        }
    }
}