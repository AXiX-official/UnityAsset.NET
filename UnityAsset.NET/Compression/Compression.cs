using SevenZip.Compression.LZMA;
using K4os.Compression.LZ4;
using UnityAsset.NET.Enums;

namespace UnityAsset.NET;

public static class Compression
{
    public static void DecompressToStream(ReadOnlySpan<byte> compressedData, Stream decompressedStream,
        long decompressedSize, CompressionType compressionType)
    {
        switch (compressionType)
        {
            case CompressionType.None:
                decompressedStream.Write(compressedData);
                break;
            case CompressionType.Lz4:
            case CompressionType.Lz4HC:
                byte[] decompressedData = new byte[decompressedSize];
                var size = LZ4Codec.Decode(compressedData, new Span<byte>(decompressedData));
                if (size != decompressedSize)
                    throw new Exception($"Decompressed size mismatch, expected {decompressedSize}, got {size}");
                decompressedStream.Write(decompressedData, 0, decompressedData.Length);
                break;
            case CompressionType.Lzma:
                var properties = new byte[5];
                if (compressedData.Length < 5)
                    throw new Exception("input .lzma is too short");
                compressedData.Slice(0, 5).CopyTo(properties);
                var decoder = new Decoder();
                decoder.SetDecoderProperties(properties);
                MemoryStream compressedStream = new MemoryStream(compressedData.Slice(5).ToArray());
                decoder.Code(compressedStream, decompressedStream, compressedData.Length - 5, decompressedSize, null);
                break;
            default:
                throw new ArgumentException($"Unsupported compression type {compressionType}");
        }
        decompressedStream.Position = 0;
    }
    
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
    
    public static long CompressStream(ReadOnlySpan<byte> uncompressedData, MemoryStream compressedStream, CompressionType compressionType)
    {
        switch (compressionType)
        {
            case CompressionType.None:
                compressedStream.Write(uncompressedData);
                return uncompressedData.Length;
            case CompressionType.Lz4:
                byte[] compressedData = new byte[LZ4Codec.MaximumOutputSize(uncompressedData.Length)];
                int compressedSize = LZ4Codec.Encode(uncompressedData, compressedData);
                compressedStream.Write(compressedData[..compressedSize]);
                return compressedSize;
            case CompressionType.Lz4HC:
                byte[] compressedDataHc = new byte[LZ4Codec.MaximumOutputSize(uncompressedData.Length)];
                int compressedSizeHc = LZ4Codec.Encode(uncompressedData, compressedDataHc, LZ4Level.L12_MAX);
                compressedStream.Write(compressedDataHc[..compressedSizeHc]);
                return compressedSizeHc;
            case CompressionType.Lzma:
                var encoder = new Encoder();
                MemoryStream subStream = new MemoryStream();
                encoder.WriteCoderProperties(compressedStream);
                MemoryStream uncompressedStream = new MemoryStream(uncompressedData.ToArray());
                encoder.Code(uncompressedStream, subStream, -1, -1, null);
                subStream.Position = 0;
                subStream.CopyTo(compressedStream);
                return subStream.Length + 5;
            default:
                throw new ArgumentException($"Unsupported compression type {compressionType}");
        }
    }
}