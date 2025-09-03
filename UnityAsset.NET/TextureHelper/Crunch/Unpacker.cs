using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace UnityAsset.NET.TextureHelper.Crunch;

public static partial class Crunch
{
    private struct BlockBufferElement
    {
        public UInt16 EndpointReference;
        public UInt16 ColorEndpointIndex;
        public UInt16 Alpha0EndpointIndex;
        public UInt16 Alpha1EndpointIndex;
    }
    
    public sealed class Unpacker
    { 
        public Header Header;
        public byte[] Data;

        private SymbolCodec _codec = new();
        
        private StaticHuffmanDataModel _referenceEncodingDm = new();
        
        private StaticHuffmanDataModel[] _endpointDeltaDm = new StaticHuffmanDataModel[]
        {
            new (),
            new ()
        };
        private StaticHuffmanDataModel[] _selectorDeltaDm = new StaticHuffmanDataModel[]
        {
            new (),
            new ()
        };
        
        private UInt32[] _colorEndpoints = [];
        private UInt32[] _colorSelectors = [];

        private UInt16[] _alphaEndpoints = [];
        private UInt16[] _alphaSelectors = [];

        private BlockBufferElement[] _blockBuffer = [];

        public Unpacker(byte[] data)
        {
            Header = new Header(data);
            Data = data;
            
            InitTables();
            DecodePalettes();
        }
        
        public void UnpackLevel(byte[][] dst, UInt32 dstSizeInBytes, UInt32 rowPitchInBytes, UInt32 levelIndex)
        {
            UInt32 curLevelOfs = Header.LevelOfs[levelIndex];

            UInt32 nextLevelOfs = (UInt32)Data.Length;
            if (levelIndex + 1 < Header.Levels)
                nextLevelOfs = Header.LevelOfs[levelIndex + 1];
            
            Debug.Assert(nextLevelOfs > curLevelOfs);
            
            UnpackLevel(Data, curLevelOfs, nextLevelOfs - curLevelOfs, dst, dstSizeInBytes, rowPitchInBytes, levelIndex);
        }

        public void UnpackLevel(byte[] src, UInt32 offset, UInt32 srcSizeInBytes, 
            byte[][] dst, UInt32 dstSizeInBytes, UInt32 rowPitchInBytes, 
            UInt32 levelIndex)
        {
            
            UInt32 width = Math.Max(Header.Width >> (int)levelIndex, 1);
            UInt32 height = Math.Max(Header.Height >> (int)levelIndex, 1);
            UInt32 blocksX = (width + 3) >> 2;
            UInt32 blocksY = (height + 3) >> 2;
            UInt32 blockSize = (CrnFmt)(UInt32)Header.Format switch
            {
                CrnFmt.DXT1 => 8,
                CrnFmt.DXT5A => 8,
                CrnFmt.ETC1 => 8,
                CrnFmt.ETC2 => 8,
                CrnFmt.ETC1S => 8,
                _ => 16
            };
            
            UInt32 minimalRowPitch = blockSize * blocksX;
            if (rowPitchInBytes == 0)
                rowPitchInBytes = minimalRowPitch;
            else if (rowPitchInBytes < minimalRowPitch || (rowPitchInBytes & 3) != 0)
                throw new Exception("rowPitchInBytes is too small");
            if (dstSizeInBytes < rowPitchInBytes * blocksY)
                throw new Exception("dstSizeInBytes is too small");
            
            _codec.StartDecoding(src, offset, srcSizeInBytes);
            

            switch ((CrnFmt)(UInt32)Header.Format)
            {
                case CrnFmt.DXT1:
                case CrnFmt.ETC1S:
                {
                    UnpackDxt1(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                }
                case CrnFmt.DXT5:
                case CrnFmt.DXT5_CCxY:
                case CrnFmt.DXT5_xGBR:
                case CrnFmt.DXT5_AGBR:
                case CrnFmt.DXT5_xGxR:
                case CrnFmt.ETC2AS:
                {
                    UnpackDxt5(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                }
                case CrnFmt.DXT5A:
                    UnpackDxt5a(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                case CrnFmt.DXN_XY:
                case CrnFmt.DXN_YX:
                {
                    UnpackDxn(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                }
                case CrnFmt.ETC1:
                    UnpackEtc1(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                case CrnFmt.ETC2:
                    UnpackEtc1(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                case CrnFmt.ETC2A:
                    UnpackEtc2a(dst, rowPitchInBytes, blocksX, blocksY);
                    break;
                default:
                    throw new Exception("Unsupported format");
            }
        }

        private unsafe void UnpackDxt1(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numColorEndpoints = _colorEndpoints.Length;
            UInt32 width = (UInt32)(outputWidth + 1 & ~1);
            UInt32 height = (UInt32)(outputHeight + 1 & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 1));
            
            if (_blockBuffer.Length < width)
                _blockBuffer = new BlockBufferElement[width];

            UInt32 colorEndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 2)
                        {
                            if ((y & 1) == 0 && (x & 1) == 0)
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                            ref BlockBufferElement buffer = ref _blockBuffer[x];
                            byte endpointReference;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                endpointReference = (byte)(referenceGroup & 3);
                                referenceGroup >>= 2;
                                buffer.EndpointReference = (UInt16)(referenceGroup & 3);
                                referenceGroup >>= 2;
                            }

                            if (endpointReference == 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            }
                            else if (endpointReference == 1)
                            {
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            }
                            else
                            {
                                colorEndpointIndex = buffer.ColorEndpointIndex;
                            }

                            UInt32 colorSelectorIndex = _codec.Decode(_selectorDeltaDm[0]);
                            if (y < outputHeight && x < outputWidth)
                            {
                                pRow[0] = _colorEndpoints[colorEndpointIndex];
                                pRow[1] = _colorSelectors[colorSelectorIndex];
                            }
                        }
                    }
                }
            }
        }
        
        private unsafe void UnpackDxt5(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numColorEndpoints = _colorEndpoints.Length;
            var numAlphaEndpoints = _alphaEndpoints.Length;
            UInt32 width = (UInt32)((outputWidth + 1) & ~1);
            UInt32 height = (UInt32)((outputHeight + 1) & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 2));
            
            if (_blockBuffer.Length < width)
                _blockBuffer = new BlockBufferElement[width];

            UInt32 colorEndpointIndex = 0;
            UInt32 alpha0EndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 4)
                        {
                            if ((y & 1) == 0 && (x & 1) == 0)
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                            ref BlockBufferElement buffer = ref _blockBuffer[x];
                            byte endpointReference;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                endpointReference = (byte)(referenceGroup & 3);
                                referenceGroup >>= 2;
                                buffer.EndpointReference = (UInt16)(referenceGroup & 3);
                                referenceGroup >>= 2;
                            }

                            if (endpointReference == 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                                alpha0EndpointIndex += _codec.Decode(_endpointDeltaDm[1]);
                                if (alpha0EndpointIndex >= numAlphaEndpoints)
                                    alpha0EndpointIndex -= (UInt32)numAlphaEndpoints;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else if (endpointReference == 1)
                            {
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else
                            {
                                colorEndpointIndex = buffer.ColorEndpointIndex;
                                alpha0EndpointIndex = buffer.Alpha0EndpointIndex;
                            }

                            var colorSelectorIndex = _codec.Decode(_selectorDeltaDm[0]);
                            var alpha0SelectorIndex = _codec.Decode(_selectorDeltaDm[1]);
                            if (y < outputHeight && x < outputWidth)
                            {
                                var pAlpha0Selectors = alpha0SelectorIndex * 3;
                                pRow[0] = (UInt32)(_alphaEndpoints[(int)alpha0EndpointIndex] |
                                                   (_alphaSelectors[(int)pAlpha0Selectors] << 16));
                                pRow[1] = (UInt32)(_alphaSelectors[(int)(pAlpha0Selectors + 1)] |
                                                   (_alphaSelectors[(int)(pAlpha0Selectors + 2)] << 16));
                                pRow[2] = _colorEndpoints[(int)colorEndpointIndex];
                                pRow[3] = _colorSelectors[(int)colorSelectorIndex];
                            }
                        }
                    }
                }
            }
        }

        private unsafe void UnpackDxt5a(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numAlphaEndpoints = _alphaEndpoints.Length;
            UInt32 width = (UInt32)(outputWidth + 1 & ~1);
            UInt32 height = (UInt32)(outputHeight + 1 & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 1));
            
            if (_blockBuffer.Length < width)
                _blockBuffer = new BlockBufferElement[width];

            UInt32 alpha0EndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 2)
                        {
                            if ((y & 1) == 0 && (x & 1) == 0)
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                            ref BlockBufferElement buffer = ref _blockBuffer[x];
                            byte endpointReference;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                endpointReference = (byte)(referenceGroup & 3);
                                referenceGroup >>= 2;
                                buffer.EndpointReference = (UInt16)(referenceGroup & 3);
                                referenceGroup >>= 2;
                            }

                            if (endpointReference == 0)
                            {
                                alpha0EndpointIndex += _codec.Decode(_endpointDeltaDm[1]);
                                if (alpha0EndpointIndex >= numAlphaEndpoints)
                                    alpha0EndpointIndex -= (UInt32)numAlphaEndpoints;
                                buffer.ColorEndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else if (endpointReference == 1)
                            {
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else
                            {
                                alpha0EndpointIndex = buffer.Alpha0EndpointIndex;
                            }

                            UInt32 alpha0SelectorIndex = _codec.Decode(_selectorDeltaDm[1]);
                            if (y < outputHeight && x < outputWidth)
                            {
                                var pAlpha0Selectors = alpha0SelectorIndex * 3;
                                pRow[0] = (UInt32)(_alphaSelectors[alpha0SelectorIndex] |
                                                   (_alphaSelectors[pAlpha0Selectors] << 16));
                                pRow[1] = (UInt32)(_alphaSelectors[pAlpha0Selectors + 1] |
                                                   (_alphaSelectors[pAlpha0Selectors + 2] << 16));
                            }
                        }
                    }
                }
            }
        }
        
        private unsafe void UnpackDxn(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numAlphaEndpoints = _alphaEndpoints.Length;
            UInt32 width = (UInt32)(outputWidth + 1 & ~1);
            UInt32 height = (UInt32)(outputHeight + 1 & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 2));
            
            if (_blockBuffer.Length < width)
                _blockBuffer = new BlockBufferElement[width];

            UInt32 alpha0EndpointIndex = 0;
            UInt32 alpha1EndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 4)
                        {
                            if ((y & 1) == 0 && (x & 1) == 0)
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                            ref BlockBufferElement buffer = ref _blockBuffer[x];
                            byte endpointReference;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                endpointReference = (byte)(referenceGroup & 3);
                                referenceGroup >>= 2;
                                buffer.EndpointReference = (UInt16)(referenceGroup & 3);
                                referenceGroup >>= 2;
                            }

                            if (endpointReference == 0)
                            {
                                alpha0EndpointIndex += _codec.Decode(_endpointDeltaDm[1]);
                                if (alpha0EndpointIndex >= numAlphaEndpoints)
                                    alpha0EndpointIndex -= (UInt32)numAlphaEndpoints;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                                alpha1EndpointIndex += _codec.Decode(_endpointDeltaDm[1]);
                                if (alpha1EndpointIndex >= numAlphaEndpoints)
                                    alpha1EndpointIndex -= (UInt32)numAlphaEndpoints;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha1EndpointIndex;
                            }
                            else if (endpointReference == 1)
                            {
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                                buffer.Alpha1EndpointIndex = (UInt16)alpha1EndpointIndex;
                            }
                            else
                            {
                                alpha0EndpointIndex = buffer.Alpha0EndpointIndex;
                                alpha1EndpointIndex = buffer.Alpha1EndpointIndex;
                            }

                            UInt32 alpha0SelectorIndex = _codec.Decode(_selectorDeltaDm[1]);
                            UInt32 alpha1SelectorIndex = _codec.Decode(_selectorDeltaDm[1]);
                            if (y < outputHeight && x < outputWidth)
                            {
                                var pAlpha0Selectors = alpha0SelectorIndex * 3;
                                var pAlpha1Selectors = alpha1SelectorIndex * 3;
                                pRow[0] = (UInt32)(_alphaEndpoints[alpha0EndpointIndex] |
                                                   (_alphaSelectors[pAlpha0Selectors] << 16));
                                pRow[1] = (UInt32)(_alphaSelectors[pAlpha0Selectors + 1] |
                                                   (_alphaSelectors[pAlpha0Selectors + 2] << 16));
                                pRow[2] = (UInt32)(_alphaEndpoints[alpha1EndpointIndex] |
                                                   (_alphaSelectors[pAlpha1Selectors] << 16));
                                pRow[3] = (UInt32)(_alphaSelectors[pAlpha1Selectors + 1] |
                                                   (_alphaSelectors[pAlpha1Selectors + 2] << 16));
                            }
                        }
                    }
                }
            }
        }
        
        private unsafe void UnpackEtc1(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numColorEndpoints = _colorEndpoints.Length;
            UInt32 width = (UInt32)(outputWidth + 1 & ~1);
            UInt32 height = (UInt32)(outputHeight + 1 & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 1));
            
            if (_blockBuffer.Length < (width << 1))
                _blockBuffer = new BlockBufferElement[width << 1];

            UInt32 colorEndpointIndex = 0;
            UInt32 diagonalColorEndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 2)
                        {
                            var buffer = _blockBuffer[x << 1];
                            byte endpointReference;
                            EndianPackedUint blockEndpoint = 0;
                            EndianPackedUint e0 = 0;
                            EndianPackedUint e1 = 0;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                                endpointReference = (byte)((referenceGroup & 3) | (referenceGroup >> 2 & 12));
                                buffer.EndpointReference =
                                    (UInt16)((referenceGroup >> 2 & 3) | (referenceGroup >> 4 & 12));
                            }

                            if ((endpointReference & 3) == 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            }
                            else if ((endpointReference & 3) == 1)
                            {
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            }
                            else if ((endpointReference & 3) == 3)
                            {
                                colorEndpointIndex = diagonalColorEndpointIndex;
                                buffer.EndpointReference = (UInt16)colorEndpointIndex;
                            }
                            else
                            {
                                colorEndpointIndex = buffer.ColorEndpointIndex;
                            }

                            endpointReference >>= 2;
                            e0 = _colorEndpoints[colorEndpointIndex];

                            UInt32 selectorIndex = _codec.Decode(_selectorDeltaDm[0]);
                            if (endpointReference != 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                            }

                            diagonalColorEndpointIndex = _blockBuffer[x << 1 | 1].ColorEndpointIndex;
                            _blockBuffer[x << 1 | 1].ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            if (y < outputHeight && x < outputWidth)
                            {
                                UInt32 flip = (UInt32)(endpointReference >> 1 ^ 1);
                                UInt32 diff = 1;
                                for (int c = 0; (diff & c) < 3; c++)
                                    diff = ((e0[c] + 3 >= e1[c]) && (e1[c] + 4 >= e0[c])) ? diff : 0;
                                for (int c = 0; c < 3; c++)
                                    blockEndpoint[c] = diff != 0
                                        ? (byte)(e0[c] << 3 | ((e1[c] - e0[c]) & 7))
                                        : (byte)((e0[c] << 3 & 0xF0) | e1[c] >> 1);
                                blockEndpoint[3] = (byte)(e0[3] << 5 | e1[3] << 2 | (int)(diff << 1) | (int)flip);
                                pRow[0] = blockEndpoint;
                                pRow[1] = _colorSelectors[selectorIndex << 1 | flip];
                            }
                        }
                    }
                }
            }
        }
        
        private unsafe void UnpackEtc2a(byte[][] dst, UInt32 outputPitchInBytes, UInt32 outputWidth, UInt32 outputHeight)
        {
            var numColorEndpoints = _colorEndpoints.Length;
            var numAlphaEndpoints = _alphaEndpoints.Length;
            UInt32 width = (UInt32)(outputWidth + 1 & ~1);
            UInt32 height = (UInt32)(outputHeight + 1 & ~1);
            Int32 deltaPitchInDwords = (Int32)((outputPitchInBytes >> 2) - (width << 2));
            
            if (_blockBuffer.Length < (width << 1))
                _blockBuffer = new BlockBufferElement[width << 1];

            UInt32 colorEndpointIndex = 0;
            UInt32 diagonalColorEndpointIndex = 0;
            UInt32 alpha0EndpointIndex = 0;
            UInt32 diagonalAlpha0EndpointIndex = 0;
            byte referenceGroup = 0;

            for (int f = 0; f < Header.Faces; f++)
            {
                fixed (byte* pData = dst[f])
                {
                    var pRow = (UInt32*)pData;
                    for (int y = 0; y < height; y++, pRow += deltaPitchInDwords)
                    {
                        for (int x = 0; x < width; x++, pRow += 4)
                        {
                            var buffer = _blockBuffer[x << 1];
                            byte endpointReference;
                            EndianPackedUint blockEndpoint = 0;
                            EndianPackedUint e0 = 0;
                            EndianPackedUint e1 = 0;
                            if ((y & 1) != 0)
                            {
                                endpointReference = (byte)buffer.EndpointReference;
                            }
                            else
                            {
                                referenceGroup = (byte)_codec.Decode(_referenceEncodingDm);
                                endpointReference = (byte)((referenceGroup & 3) | (referenceGroup >> 2 & 12));
                                buffer.EndpointReference =
                                    (UInt16)((referenceGroup >> 2 & 3) | (referenceGroup >> 4 & 12));
                            }

                            if ((endpointReference & 3) == 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                                alpha0EndpointIndex += _codec.Decode(_endpointDeltaDm[1]);
                                if (alpha0EndpointIndex >= numAlphaEndpoints)
                                    alpha0EndpointIndex -= (UInt32)numAlphaEndpoints;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else if ((endpointReference & 3) == 1)
                            {
                                buffer.ColorEndpointIndex = (UInt16)colorEndpointIndex;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else if ((endpointReference & 3) == 3)
                            {
                                colorEndpointIndex = diagonalColorEndpointIndex;
                                buffer.EndpointReference = (UInt16)colorEndpointIndex;
                                alpha0EndpointIndex = diagonalAlpha0EndpointIndex;
                                buffer.Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            }
                            else
                            {
                                colorEndpointIndex = buffer.ColorEndpointIndex;
                                alpha0EndpointIndex = buffer.Alpha0EndpointIndex;
                            }

                            endpointReference >>= 2;
                            e0 = _colorEndpoints[colorEndpointIndex];

                            UInt32 colorSelectorIndex = _codec.Decode(_selectorDeltaDm[0]);
                            UInt32 alpha0SelectorIndex = _codec.Decode(_selectorDeltaDm[1]);
                            if (endpointReference != 0)
                            {
                                colorEndpointIndex += _codec.Decode(_endpointDeltaDm[0]);
                                if (colorEndpointIndex >= numColorEndpoints)
                                    colorEndpointIndex -= (UInt32)numColorEndpoints;
                            }
                            
                            e1 = _colorEndpoints[colorEndpointIndex];

                            diagonalColorEndpointIndex = _blockBuffer[x << 1 | 1].ColorEndpointIndex;
                            _blockBuffer[x << 1 | 1].ColorEndpointIndex = (UInt16)colorEndpointIndex;
                            diagonalAlpha0EndpointIndex = _blockBuffer[x << 1 | 1].Alpha0EndpointIndex;
                            _blockBuffer[x << 1 | 1].Alpha0EndpointIndex = (UInt16)alpha0EndpointIndex;
                            if (y < outputHeight && x < outputWidth)
                            {
                                UInt32 flip = (UInt32)(endpointReference >> 1 ^ 1);
                                UInt32 diff = 1;
                                for (int c = 0; (diff & c) < 3; c++)
                                    diff = ((e0[c] + 3 >= e1[c]) && (e1[c] + 4 >= e0[c])) ? diff : 0;
                                for (int c = 0; c < 3; c++)
                                    blockEndpoint[c] = diff != 0
                                        ? (byte)(e0[c] << 3 | ((e1[c] - e0[c]) & 7))
                                        : (byte)((e0[c] << 3 & 0xF0) | e1[c] >> 1);
                                blockEndpoint[3] = (byte)(e0[3] << 5 | e1[3] << 2 | (int)(diff << 1) | (int)flip);
                                var pAlpha0Selectors = alpha0SelectorIndex * 6 + (flip != 0 ? 3 : 0);
                                pRow[0] = (UInt32)(_alphaEndpoints[alpha0EndpointIndex] |
                                                   (_alphaSelectors[pAlpha0Selectors] << 16));
                                pRow[1] = (UInt32)(_alphaSelectors[pAlpha0Selectors + 1] |
                                                   (_alphaSelectors[pAlpha0Selectors + 2] << 16));
                                pRow[2] = blockEndpoint;
                                pRow[3] = _colorSelectors[colorSelectorIndex << 1 | flip];
                            }
                        }
                    }
                }
            }
        }

        private void InitTables()
        {
            _codec.StartDecoding(Data, Header.TablesOfs, Header.TablesSize);
            _codec.DecodeReceiveStaticDataModel(_referenceEncodingDm);
            if (Header.ColorEndpoints.Num == 0 && Header.AlphaEndpoints.Num == 0)
                throw new Exception("no endpoints in InitTables");
            if (Header.ColorEndpoints.Num != 0)
            {
                _codec.DecodeReceiveStaticDataModel(_endpointDeltaDm[0]);
                _codec.DecodeReceiveStaticDataModel(_selectorDeltaDm[0]);
            }
            if (Header.AlphaEndpoints.Num != 0)
            {
                _codec.DecodeReceiveStaticDataModel(_endpointDeltaDm[1]);
                _codec.DecodeReceiveStaticDataModel(_selectorDeltaDm[1]);
            }
        }

        private void DecodePalettes()
        {
            if (Header.ColorEndpoints.Num != 0)
            {
                DecodeColorEndpoints();
                DecodeColorSelectors();
            }
            if (Header.AlphaEndpoints.Num != 0)
            {
                DecodeAlphaEndpoints();
                switch ((CrnFmt)(UInt32)Header.Format)
                {
                    case CrnFmt.ETC2AS:
                        DecodeAlphaSelectorsEtcs();
                        break;
                    case CrnFmt.ETC2A:
                        DecodeAlphaSelectorsEtc();
                        break;
                    default:
                        DecodeAlphaSelectors();
                        break;
                }
            }
        }

        private void DecodeColorEndpoints()
        {
            UInt32 numColorEndpoints = Header.ColorEndpoints.Num;
            bool hasEtcColorBlocks = (CrnFmt)(UInt32)Header.Format switch
            {
                CrnFmt.ETC1 => true,
                CrnFmt.ETC2 => true,
                CrnFmt.ETC2A => true,
                CrnFmt.ETC1S => true,
                CrnFmt.ETC2AS => true,
                _ => false,
            };
            bool hasSubblocks = (CrnFmt)(UInt32)Header.Format switch
            {
                CrnFmt.ETC1 => true,
                CrnFmt.ETC2 => true,
                CrnFmt.ETC2A => true,
                _ => false,
            };
            
            _colorEndpoints = new UInt32[numColorEndpoints];
            _codec.StartDecoding(Data, Header.ColorEndpoints.Ofs, Header.ColorEndpoints.Size);

            var dm = new StaticHuffmanDataModel[]
            {
                new(),
                new()
            };
            _codec.DecodeReceiveStaticDataModel(dm[0]);
            _codec.DecodeReceiveStaticDataModel(dm[1]);

            UInt32 a = 0, b = 0, c = 0;
            UInt32 d = 0, e = 0, f = 0;

            var dst = 0;

            for (int i = 0; i < numColorEndpoints; i++)
            {
                if (hasEtcColorBlocks)
                {
                    for (b = 0; b < 32; b += 8)
                        a += _codec.Decode(dm[0]) << (int)b;
                    a &= 0x1F1F1F1F;
                    _colorEndpoints[dst++] = hasSubblocks
                        ? a 
                        : (a & 0x07000000) << 5 | (a & 0x07000000) << 2 | 0x02000000 | (a & 0x001F1F1F) << 3;
                }
                else
                {
                    a = (a + _codec.Decode(dm[0])) & 31;
                    b = (b + _codec.Decode(dm[1])) & 63;
                    c = (c + _codec.Decode(dm[0])) & 31;
                    d = (d + _codec.Decode(dm[0])) & 31;
                    e = (e + _codec.Decode(dm[1])) & 63;
                    f = (f + _codec.Decode(dm[0])) & 31;

                    _colorEndpoints[dst++] = c | (b << 5) | (a << 11) | (f << 16) | (e << 21) | (d << 27);
                }
            }
        }

        private void DecodeColorSelectors()
        {
            bool hasEtcColorBlocks = (CrnFmt)(UInt32)Header.Format switch
            {
                CrnFmt.ETC1 => true,
                CrnFmt.ETC2 => true,
                CrnFmt.ETC2A => true,
                CrnFmt.ETC1S => true,
                CrnFmt.ETC2AS => true,
                _ => false,
            };
            bool hasSubblocks = (CrnFmt)(UInt32)Header.Format switch
            {
                CrnFmt.ETC1 => true,
                CrnFmt.ETC2 => true,
                CrnFmt.ETC2A => true,
                _ => false,
            };
            
            _codec.StartDecoding(Data, Header.ColorSelectors.Ofs, Header.ColorSelectors.Size);

            var dm = new StaticHuffmanDataModel();
            _codec.DecodeReceiveStaticDataModel(dm);

            _colorSelectors = new UInt32[Header.ColorSelectors.Num << (hasSubblocks ? 1 : 0)];

            for (UInt32 s = 0, i = 0; i < Header.ColorSelectors.Num; i++)
            {
                for (int j = 0; j < 32; j += 4)
                    s ^= _codec.Decode(dm) << j;

                if (hasEtcColorBlocks)
                {
                    for (UInt32 selector = (~s & 0xAAAAAAAA) | (~(s ^ s >> 1) & 0x55555555), t = 8, h = 0;
                         h < 4;
                         h++, t -= 15)
                    {
                        for (UInt32 w = 0; w < 4; w++, t += 4)
                        {
                            if (hasSubblocks)
                            {
                                UInt32 s0 = selector >> (int)(w << 3 | h << 1);
                                _colorSelectors[i << 1] = ((s0 >> 1 & 1) | (s0 & 1) << 16) << (int)(t & 15);
                            }
                            UInt32 s1 = selector >> (int)(h << 3 | w << 1);
                            _colorSelectors[hasSubblocks ? i << 1 | 1 : i] |= ((s1 >> 1 & 1) | (s1 & 1) << 16) << (int)(t & 15);
                        }
                    }
                }
                else
                {
                    _colorSelectors[i] = ((s ^ s << 1) & 0xAAAAAAAA) | (s >> 1 & 0x55555555);
                }
            }
        }
        
        private void DecodeAlphaEndpoints()
        {
            UInt32 numAlphaEndpoints = Header.AlphaEndpoints.Num;
            
            _codec.StartDecoding(Data, Header.AlphaEndpoints.Ofs, Header.AlphaEndpoints.Size);
            
            var dm = new StaticHuffmanDataModel();
            _codec.DecodeReceiveStaticDataModel(dm);
            
            _alphaEndpoints = new UInt16[numAlphaEndpoints];

            var dst = 0;
            UInt32 a = 0, b = 0;
            
            for (int i = 0; i < numAlphaEndpoints; i++)
            {
                var sa = _codec.Decode(dm);
                var sb = _codec.Decode(dm);
                a = (a + sa) & 255;
                b = (b + sb) & 255;

                _alphaEndpoints[dst++] = (UInt16)(a | (b << 8));
            }
        }

        private void DecodeAlphaSelectors()
        {
            _codec.StartDecoding(Data, Header.AlphaSelectors.Ofs, Header.AlphaSelectors.Size);
            var dm = new StaticHuffmanDataModel();
            _codec.DecodeReceiveStaticDataModel(dm);
            _alphaSelectors = new UInt16[Header.AlphaSelectors.Num * 3];
            Span<byte> dxt5FromLinear = stackalloc byte[64];
            for (int i = 0; i < 64; i++)
                dxt5FromLinear[i] = (byte)(Dxt5FromLinear[i & 7] | Dxt5FromLinear[i >> 3] << 3);
            for (UInt32 s0Linear = 0, s1Linear = 0, i = 0; i < _alphaSelectors.Length;)
            {
                UInt32 s0 = 0, s1 = 0;
                for (int j = 0; j < 24; s0 |= (UInt32)(dxt5FromLinear[(int)((s0Linear >> j) & 0x3F)] << j), j += 6)
                    s0Linear ^= _codec.Decode(dm) << j;
                for (int j = 0; j < 24; s1 |= (UInt32)(dxt5FromLinear[(int)((s1Linear >> j) & 0x3F)] << j), j += 6)
                    s1Linear ^= _codec.Decode(dm) << j;
                _alphaSelectors[i++] = (UInt16)s0;
                _alphaSelectors[i++] = (UInt16)(s0 >> 16 | s1 << 8);
                _alphaSelectors[i++] = (UInt16)(s1 >> 8);
            }
        }
        
        private void DecodeAlphaSelectorsEtc()
        {
            _codec.StartDecoding(Data, Header.AlphaSelectors.Ofs, Header.AlphaSelectors.Size);
            var dm = new StaticHuffmanDataModel();
            _codec.DecodeReceiveStaticDataModel(dm);
            _alphaSelectors = new UInt16[Header.AlphaSelectors.Num * 6];
            Span<byte> sLinear = stackalloc byte[8];
            var pData = 0;
            for (int i = 0; i < _alphaSelectors.Length; i += 6, pData += 12)
            {
                for (int sGroup = 0, p = 0; p < 16; p++)
                {
                    sGroup = (p & 1) != 0 ? sGroup >> 3 : sLinear[p >> 1] ^= (byte)_codec.Decode(dm);
                    byte s = (byte)(sGroup & 7);
                    if (s <= 3)
                        s = (byte)(3 - s);
                    byte d = (byte)(3 * (p + 1));
                    byte byteOffset = (byte)(d >> 3);
                    byte bitOffset = (byte)(d & 7);
                    _alphaSelectors[pData + byteOffset] |= (UInt16)(s << (8 - bitOffset));
                    if (bitOffset < 3)
                        _alphaSelectors[pData + byteOffset - 1] |= (UInt16)(s >> bitOffset);
                    d += (byte)(9 * ((p & 3) - (p >> 2)));
                    byteOffset = (byte)(d >> 3);
                    bitOffset = (byte)(d & 7);
                    _alphaSelectors[pData + byteOffset + 6] |= (UInt16)(s << (8 - bitOffset));
                    if (bitOffset < 3)
                        _alphaSelectors[pData + byteOffset + 5] |= (UInt16)(s >> bitOffset);
                }
            }
        }
        
        private void DecodeAlphaSelectorsEtcs()
        {
            _codec.StartDecoding(Data, Header.AlphaSelectors.Ofs, Header.AlphaSelectors.Size);
            var dm = new StaticHuffmanDataModel();
            _codec.DecodeReceiveStaticDataModel(dm);
            _alphaSelectors = new UInt16[Header.AlphaSelectors.Num * 3];
            Span<byte> sLinear = stackalloc byte[8];
            for (int i = 0; i < _alphaSelectors.Length; i += 6)
            {
                for (int sGroup = 0, p = 0; p < 16; p++)
                {
                    sGroup = (p & 1) != 0 ? sGroup >> 3 : sLinear[p >> 1] ^= (byte)_codec.Decode(dm);
                    byte s = (byte)(sGroup & 7);
                    if (s <= 3)
                        s = (byte)(3 - s);
                    byte d = (byte)(3 * (p + 1) + 9 * ((p & 3) - (p >> 2)));
                    byte byteOffset = (byte)(d >> 3);
                    byte bitOffset = (byte)(d & 7);
                    _alphaSelectors[i + byteOffset] |= (UInt16)(s << (8 - bitOffset));
                    if (bitOffset < 3)
                        _alphaSelectors[i + byteOffset - 1] |= (UInt16)(s >> bitOffset);
                }
            }
        }
    }
}