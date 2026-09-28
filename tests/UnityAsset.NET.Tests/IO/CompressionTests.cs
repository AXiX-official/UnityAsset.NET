using UnityAsset.NET.Extensions;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// <see cref="Compression"/>, which sits under both the bundle block reader and the serialized-file metadata path:
/// every payload that comes off disk goes through one of these four branches. The cases check the round trip for each
/// type, the failure modes for malformed input, and the two behaviours that are easy to miss — that
/// <c>None</c> requires matching lengths and that the byte-array and stream entry points produce different wrappers
/// for LZMA.
/// </summary>
public sealed class CompressionTests
{
    public static TheoryData<CompressionType> AllTypes => new()
    {
        CompressionType.None,
        CompressionType.Lz4,
        CompressionType.Lz4HC,
        CompressionType.Lzma,
    };

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void RoundTrip_ThroughTheSpanEntryPoints_ReturnsTheOriginalBytes(CompressionType type)
    {
        var original = Payload(1024);

        var compressed = new byte[CompressionBound(type, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, type);
        Assert.True(compressedSize > 0);

        var decompressed = new byte[original.Length];
        Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, type);

        Assert.Equal(original, decompressed);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void RoundTrip_OfASmallPayload_ReturnsTheOriginalBytes(CompressionType type)
    {
        // A payload below the LZMA header size and below one LZ4 block is the case that hits the size checks.
        var original = Payload(3);

        var compressed = new byte[CompressionBound(type, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, type);

        var decompressed = new byte[original.Length];
        Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, type);

        Assert.Equal(original, decompressed);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void RoundTrip_OfAnEmptyPayload_ReturnsNothing(CompressionType type)
    {
        var original = Array.Empty<byte>();

        var compressed = new byte[CompressionBound(type, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, type);

        var decompressed = Array.Empty<byte>();
        Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, type);

        Assert.Empty(decompressed);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void RoundTrip_OfHighlyCompressibleData_ReturnsTheOriginalBytes(CompressionType type)
    {
        // Repetitive input is where an LZ4/LZMA block actually shrinks, so the compressed buffer is much smaller than
        // the uncompressed one. That is the shape real payloads have and the one an off-by-one in the size handling
        // would break first.
        var original = Enumerable.Repeat((byte)0xAB, 4096).ToArray();

        var compressed = new byte[CompressionBound(type, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, type);

        var decompressed = new byte[original.Length];
        Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, type);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Decompress_UncompressedPayloadOfTheWrongLength_Throws()
    {
        // None is a plain copy, so the two spans have to be the same length. Anything else is a malformed block and has
        // to be reported rather than silently truncating to that shorter of the two.
        var compressed = Payload(16);
        var decompressed = new byte[8];

        Assert.Throws<ArgumentException>(() =>
            Compression.DecompressToBytes(compressed, decompressed, CompressionType.None));
    }

    [Fact]
    public void Decompress_Lz4IntoTheWrongSize_Throws()
    {
        // The declared uncompressed size and the data disagree. LZ4 decodes what it can and the length check catches
        // the mismatch, which is what protects the block reader from a size field that lies.
        var original = Payload(256);
        var compressed = new byte[CompressionBound(CompressionType.Lz4, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, CompressionType.Lz4);

        var decompressed = new byte[128];

        Assert.Throws<Exception>(() =>
            Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, CompressionType.Lz4));
    }

    [Fact]
    public void Decompress_LzmaShorterThanItsHeader_Throws()
    {
        var tooShort = new byte[4];

        Assert.Throws<Exception>(() =>
            Compression.DecompressToBytes(tooShort, new byte[16], CompressionType.Lzma));
    }

    [Fact]
    public void Decompress_UnknownType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Compression.DecompressToBytes(Payload(4), new byte[4], (CompressionType)99));
    }

    [Fact]
    public void CompressToStream_None_IsAVerbatinCopy()
    {
        var original = Payload(32);

        using var stream = Compression.CompressToStream(original, CompressionType.None);

        Assert.Equal(original, ReadAll(stream));
    }

    [Theory]
    [InlineData(CompressionType.Lz4)]
    [InlineData(CompressionType.Lz4HC)]
    public void CompressToStream_Lz4_MatchesTheSpanEntryPoint(CompressionType type)
    {
        // Bundle metadata goes through the stream entry point while blocks go through the span one. For LZ4 the two
        // must agree byte for byte, otherwise a file written one way cannot be read the other.
        var original = Payload(512);

        var spanCompressed = new byte[CompressionBound(type, original.Length)];
        var spanSize = Compression.CompressToBytes(original, spanCompressed, type);

        using var stream = Compression.CompressToStream(original, type);
        var streamCompressed = ReadAll(stream);

        Assert.Equal(spanCompressed.AsSpan(0, (int)spanSize).ToArray(), streamCompressed);
    }

    [Fact]
    public void CompressToStream_Lzma_IsAPayloadThatCanBeReadStraightAway()
    {
        // What a caller does with the result: copy it out and decode it. The encoder left the returned stream positioned
        // at its end, so this read nothing at all while Length reported the full payload — the failure looked like an
        // empty result rather than a misplaced cursor, which is why the assertion here is the round trip and not a length.
        var original = Payload(256);

        using var stream = Compression.CompressToStream(original, CompressionType.Lzma);
        var compressed = ReadAll(stream);

        Assert.NotEmpty(compressed);
        Assert.Equal(stream.Length, compressed.Length);

        var decompressed = new byte[original.Length];
        Compression.DecompressToBytes(compressed, decompressed, CompressionType.Lzma);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressToStream_Lzma_AgreesWithTheByteArrayEntryPoint()
    {
        // Both entry points write the coder properties followed by the coded data, so a payload produced either way has
        // to decode the same and have the same length. This is what makes the two interchangeable, which the bundle
        // writer relies on when it picks one of them for the blocks-info.
        var original = Payload(256);

        var bytes = new byte[CompressionBound(CompressionType.Lzma, original.Length)];
        var byteSize = Compression.CompressToBytes(original, bytes, CompressionType.Lzma);

        using var stream = Compression.CompressToStream(original, CompressionType.Lzma);
        var fromStream = ReadAll(stream);

        Assert.Equal((int)byteSize, fromStream.Length);

        var fromBytes = new byte[original.Length];
        Compression.DecompressToBytes(bytes.AsSpan(0, (int)byteSize), fromBytes, CompressionType.Lzma);

        var fromStreamDecoded = new byte[original.Length];
        Compression.DecompressToBytes(fromStream, fromStreamDecoded, CompressionType.Lzma);

        Assert.Equal(fromBytes, fromStreamDecoded);
        Assert.Equal(original, fromStreamDecoded);
    }

    [Fact]
    public void CompressToBytes_Lzma_IsSelfContainedSoTheSpanDecoderReadsIt()
    {
        // The layout the decoder actually understands: coder properties first, then the coded data.
        var original = Payload(256);

        var compressed = new byte[CompressionBound(CompressionType.Lzma, original.Length)];
        var compressedSize = Compression.CompressToBytes(original, compressed, CompressionType.Lzma);

        var decompressed = new byte[original.Length];
        Compression.DecompressToBytes(compressed.AsSpan(0, (int)compressedSize), decompressed, CompressionType.Lzma);

        Assert.Equal(original, decompressed);
    }

    private static int CompressionBound(CompressionType type, int length)
        => Math.Max(64, type switch
        {
            CompressionType.Lzma => length * 2 + 64,
            _ => K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(length) + 16,
        });

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static byte[] Payload(int length)
    {
        // A pattern rather than a constant, so a codec that "works" only on uniform data does not pass.
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)((i * 31 + (i >> 3)) & 0xFF);

        return payload;
    }
}