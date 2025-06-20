﻿using SevenZip.Compression.LZMA;
using K4os.Compression.LZ4;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET;

public static class Compression
{
    public static void DecompressToBytes(ReadOnlySpan<byte> compressedData, Span<byte> decompressedData, CompressionType compressionType)
    {
        switch (compressionType)
        {
            case CompressionType.None:
                compressedData.CopyTo(decompressedData);
                break;
            case CompressionType.Lz4:
            case CompressionType.Lz4HC:
                var sizeLz4 = LZ4Codec.Decode(compressedData, decompressedData);
                if (sizeLz4 != decompressedData.Length)
                    throw new Exception($"Decompressed size mismatch, expected {decompressedData.Length}, got {sizeLz4}");
                break;
            case CompressionType.Lzma:
            {
                var properties = new byte[5];
                if (compressedData.Length < 5)
                    throw new Exception("input .lzma is too short");
                compressedData.Slice(0, 5).CopyTo(properties);
                var decoder = new Decoder();
                decoder.SetDecoderProperties(properties);
                MemoryStream compressedStream = new MemoryStream(compressedData.Slice(5).ToArray());
                using var decompressedStream = new MemoryStream(decompressedData.Length);
                decoder.Code(compressedStream, decompressedStream, compressedData.Length - 5, decompressedData.Length, null);
                decompressedStream.Position = 0;
                var sizeLzma = decompressedStream.Read(decompressedData);
                if (sizeLzma != decompressedData.Length)
                    throw new Exception($"Decompressed size mismatch, expected {decompressedData.Length}, got {sizeLzma}");
                break;
            }
            default:
                throw new ArgumentException($"Unsupported compression type {compressionType}");
        }
    }
    
    public static long CompressToBytes(ReadOnlySpan<byte> uncompressedData, Span<byte> compressedData, CompressionType compressionType)
    {
        switch (compressionType)
        {
            case CompressionType.None:
                uncompressedData.CopyTo(compressedData);
                return uncompressedData.Length;
            case CompressionType.Lz4:
                int compressedSize = LZ4Codec.Encode(uncompressedData, compressedData);
                return compressedSize;
            case CompressionType.Lz4HC:
                int compressedSizeHc = LZ4Codec.Encode(uncompressedData, compressedData, LZ4Level.L12_MAX);
                return compressedSizeHc;
            case CompressionType.Lzma:
            {
                var encoder = new Encoder();
                using MemoryStream compressedStream = new MemoryStream();
                using MemoryStream subStream = new MemoryStream();
                encoder.WriteCoderProperties(compressedStream);
                using MemoryStream uncompressedStream = new MemoryStream(uncompressedData.ToArray());
                encoder.Code(uncompressedStream, subStream, -1, -1, null);
                subStream.Position = 0;
                subStream.CopyTo(compressedStream);
                var size = compressedStream.Position;
                compressedStream.Position = 0;
                compressedStream.ReadExactly(compressedData.Slice(0, (int)size));
                return size;
            }
            default:
                throw new ArgumentException($"Unsupported compression type {compressionType}");
        }
    }
}