﻿using SevenZip.Compression.LZMA;
using K4os.Compression.LZ4;

namespace UnityAsset.NET;

public static class Compression
{
    public static void DecompressToStream(ReadOnlySpan<byte> compressedData, Stream decompressedStream,
        long decompressedSize, string compressionType)
    {
        switch (compressionType)
        {
            case "lz4":
                byte[] decompressedData = new byte[decompressedSize];
                var size = LZ4Codec.Decode(compressedData, new Span<byte>(decompressedData));
                if (size != decompressedSize)
                    throw new Exception($"Decompressed size mismatch, expected {decompressedSize}, got {size}");
                decompressedStream.Write(decompressedData, 0, decompressedData.Length);
                break;
            case "lzma":
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
    }
    
    public static void DecompressToBytes(ReadOnlySpan<byte> compressedData, Span<byte> decompressedData, string compressionType)
    {
        switch (compressionType)
        {
            case "lz4":
                var sizeLz4 = LZ4Codec.Decode(compressedData, decompressedData);
                if (sizeLz4 != decompressedData.Length)
                    throw new Exception($"Decompressed size mismatch, expected {decompressedData.Length}, got {sizeLz4}");
                break;
            case "lzma":
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
    
    public static List<byte> CompressStream(MemoryStream uncompressedStream, string compressionType)
    {
        byte[] uncompressedData = uncompressedStream.ToArray();
        switch (compressionType)
        {
            case "none":
                return uncompressedData.ToList();
            case "lz4":
                byte[] compressedData = new byte[LZ4Codec.MaximumOutputSize(uncompressedData.Length)];
                int compressedSize = LZ4Codec.Encode(uncompressedData, compressedData);
                return compressedData.Take(compressedSize).ToList();
            case "lz4hc":
                byte[] compressedDataHC = new byte[LZ4Codec.MaximumOutputSize(uncompressedData.Length)];
                int compressedSizeHC = LZ4Codec.Encode(uncompressedData, compressedDataHC, LZ4Level.L12_MAX);
                return compressedDataHC.Take(compressedSizeHC).ToList();
            case "lzma":
                var encoder = new Encoder();
                MemoryStream compressedStream = new MemoryStream();
                encoder.WriteCoderProperties(compressedStream);
                long fileSize = uncompressedStream.Length;
                uncompressedStream.Position = 0;
                encoder.Code(uncompressedStream, compressedStream, -1, -1, null);
                //Console.WriteLine($"Compressed {fileSize} bytes to {compressedStream.Length} bytes");
                return compressedStream.ToArray().ToList();
            default:
                throw new ArgumentException($"Unsupported compression type {compressionType}");
        }
    }
}