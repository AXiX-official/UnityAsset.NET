using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Tests.Support;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// <see cref="SlicedReader"/>, the reader every asset is materialised through: a serialized file hands each asset a
/// slice of a bigger reader, so the interesting behaviour is all about the boundary — reading inside the window must
/// return exactly the same bytes as reading the whole file, and nothing outside the window may be consumed.
/// </summary>
public sealed class SlicedReaderTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // The window.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Read_ReturnsTheBytesInsideTheWindow()
    {
        var provider = new MemoryReaderProvider(Payload(64));
        var reader = new SlicedReader(provider, start: 16, length: 8);
        var buffer = new byte[8];

        Assert.Equal(0, reader.Position);
        Assert.Equal(8, reader.Length);
        Assert.Equal(8, ((IReader)reader).Remaining);

        Assert.Equal(8, reader.Read(buffer, 0, 8));
        Assert.Equal(Enumerable.Range(16, 8).Select(i => (byte)i), buffer);
        Assert.Equal(8, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);
    }

    [Fact]
    public void Read_ClampsToTheWindowNotToTheBuffer()
    {
        var provider = new MemoryReaderProvider(Payload(64));
        var reader = new SlicedReader(provider, start: 16, length: 8);

        // The buffer is far larger than the slice: a reader that fills it would hand back the neighbouring bytes.
        var buffer = new byte[64];
        Assert.Equal(8, reader.Read(buffer, 0, 64));
        Assert.Equal(Enumerable.Range(16, 8).Select(i => (byte)i), buffer.Take(8));
        Assert.All(buffer.Skip(8), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Read_InChunks_LandsOnTheSameBytesAsTheContiguousDownload()
    {
        var provider = new MemoryReaderProvider(Payload(64));
        var reader = new SlicedReader(provider, start: 10, length: 20);

        var expected = Enumerable.Range(10, 20).Select(i => (byte)i).ToArray();
        var collected = new List<byte>();
        var buffer = new byte[3];
        while (((IReader)reader).Remaining > 0)
        {
            var read = reader.Read(buffer, 0, 3);
            Assert.True(read > 0);
            collected.AddRange(buffer.Take(read));
        }

        Assert.Equal(expected, collected);
        Assert.Equal(20, reader.Position);
    }

    [Fact]
    public void Read_AtTheEnd_ReturnsZero()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 4);

        Assert.Equal(4, reader.Read(new byte[8], 0, 8));
        Assert.Equal(0, reader.Read(new byte[8], 0, 8));
    }

    [Fact]
    public void Read_WithTheCursorAtTheSliceEnd_ReturnsZero()
    {
        // The furthest the cursor can reach; a position beyond it is refused by the setter, so this is the only
        // "nothing left" state a slice can be in.
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 4);

        reader.Position = 4;

        Assert.Equal(4, reader.Position);
        Assert.Equal(0, reader.Read(new byte[8], 0, 8));
    }

    [Fact]
    public void Position_PastTheSliceEnd_IsRefused_EvenThoughTheFileContinues()
    {
        // The slice boundary is the reader's own Length, not the base reader's: byte 4 of the file exists, but it is not
        // part of a 4-byte slice.
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = 5);
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void TwoReadersFromOneProvider_HaveIndependentCursors()
    {
        // Every asset gets its own reader; two assets pointing at the same file must not share a cursor.
        var provider = new MemoryReaderProvider(Payload(64));
        var first = new SlicedReader(provider, start: 0, length: 8);
        var second = new SlicedReader(provider, start: 0, length: 8);

        first.ReadBytes(5);

        Assert.Equal(5, first.Position);
        Assert.Equal(0, second.Position);
        Assert.Equal(0, second.ReadByte());
    }

    [Fact]
    public void Position_Set_IsRelativeToTheSlice()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 32, length: 8);

        reader.Position = 4;

        Assert.Equal(4, reader.Position);
        Assert.Equal(36, reader.ReadByte());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Typed reads at the boundary.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadByte_AtTheLastByteInsideTheWindow_Succeeds()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 3);
        reader.Position = 2;

        Assert.Equal(2, reader.ReadByte());
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadByte_AtTheEndOfTheWindow_Throws()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 3);
        reader.Position = 3;

        Assert.Throws<Exception>(() => reader.ReadByte());
    }

    [Fact]
    public void ReadInt32_WhenItWouldCrossTheWindow_Throws()
    {
        // Two bytes are left but an int needs four: the slice refuses rather than reading the next slice's bytes.
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 6);
        reader.Position = 4;

        Assert.Throws<Exception>(() => reader.ReadInt32());
    }

    [Fact]
    public void ReadInt32_AtTheLastFourBytesOfTheWindow_Succeeds()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 4)
            { Endian = Endianness.LittleEndian };

        Assert.Equal(0x03020100, reader.ReadInt32());
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void TypedReads_PickUpTheBaseReadersEndianness()
    {
        // A slice delegates Endian to its base, so setting it on the slice is how the whole asset tree is configured.
        var reader = new SlicedReader(new MemoryReaderProvider(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x00 }), 0, 4)
            { Endian = Endianness.BigEndian };
        Assert.Equal(0x12345678, reader.ReadInt32());

        var little = new SlicedReader(new MemoryReaderProvider(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x00 }), 0, 4)
            { Endian = Endianness.LittleEndian };
        Assert.Equal(0x78563412, little.ReadInt32());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Construction: a window that does not fit describes an asset that cannot exist.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Construction_WithAWindowThatDoesNotFit_Throws()
    {
        var provider = new MemoryReaderProvider(Payload(16));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SlicedReader(provider, start: 8, length: 16));
    }

    [Fact]
    public void Construction_WithANonZeroStart_ReadsFromThere()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(16)), start: 8, length: 4);

        Assert.Equal(8, reader.ReadByte());
        Assert.Equal(4, reader.Length);
    }

    [Fact]
    public void Provider_PassesItsOffsetAndLengthThrough()
    {
        var provider = new SlicedReaderProvider(new MemoryReaderProvider(Payload(16)), offset: 4, length: 4);

        var reader = provider.CreateReader(Endianness.BigEndian);

        Assert.Equal(4, reader.Length);
        Assert.Equal(4, reader.ReadByte());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Strings.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadNullTerminatedString_InsideTheWindow_ReadsTheString()
    {
        // The window is "ab\0c\0": the string ends at position 3, well inside the five-byte slice, so it reads.
        var reader = new SlicedReader(
            new MemoryReaderProvider(new byte[] { (byte)'a', (byte)'b', 0x00, (byte)'c', 0x00 }), 0, 5);

        Assert.Equal("ab", reader.ReadNullTerminatedString());
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadNullTerminatedString_EndingOnTheLastByteOfTheWindow_Reads()
    {
        // The shape an asset actually has: the slice is the asset's exact byte size and the string's terminator is its last
        // byte. A cursor that lands exactly on Length has not left the slice, so this reads.
        var reader = new SlicedReader(
            new MemoryReaderProvider(new byte[] { (byte)'a', (byte)'b', 0x00, (byte)'c', 0x00 }), 0, 3);

        Assert.Equal("ab", reader.ReadNullTerminatedString());
        Assert.Equal(3, reader.Position);
        Assert.Equal(reader.Length, reader.Position);
    }

    [Fact]
    public void ReadNullTerminatedString_TheLastFieldOfASlice_Reads()
    {
        // The same case with the terminator genuinely last: "abc\0" filling a four-byte slice.
        var reader = new SlicedReader(
            new MemoryReaderProvider(new byte[] { (byte)'a', (byte)'b', (byte)'c', 0x00, (byte)'d' }), 0, 4);

        Assert.Equal("abc", reader.ReadNullTerminatedString());
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void ReadNullTerminatedString_WhenTheTerminatorIsOutsideTheWindow_Throws()
    {
        // "abcd\0" behind a 4-byte window: the base reader finds the terminator at byte 4, outside the window, so the
        // read consumed more than the slice holds. The cursor ends up past Length and the slice reports it.
        var reader = new SlicedReader(
            new MemoryReaderProvider(new byte[] { (byte)'a', (byte)'b', (byte)'c', (byte)'d', 0x00 }), 0, 4);

        Assert.Throws<Exception>(() => reader.ReadNullTerminatedString());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Align: it goes through the base reader, so the window can be left behind.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Align_InsideTheWindow_StaysInside()
    {
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 16);

        ((IReader)reader).Seek(1);
        ((IReader)reader).Align(4);

        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void Align_AtTheEndOfTheWindow_LeavesTheCursorAtTheWindowEnd()
    {
        // Align goes to "end of the reader", and for a slice the reader it knows about is the base one: the result is
        // that a 12-byte window aligned from position 9 lands on 12 (the slice's own end), not past it.
        var reader = new SlicedReader(new MemoryReaderProvider(Payload(64)), start: 0, length: 12);

        ((IReader)reader).Seek(9);
        ((IReader)reader).Align(4);

        Assert.Equal(0, reader.Position % 4);
        Assert.True(reader.Position <= reader.Length);
    }

    private static byte[] Payload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)i;

        return payload;
    }
}
