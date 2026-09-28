using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The parts of the <see cref="IReader"/> contract that the cross-implementation matrices do not cover.
/// <para>
/// <c>ReadContractTests</c> owns the <c>Read</c> family and <c>TypedReadContractTests</c> owns the fixed-width readers,
/// both across every implementation. What is left here is the surface they do not reach: the string members, the
/// collection members, <c>Seek</c> origins, and the single-reader details of <c>Align</c> at the end of the data.
/// Keeping the duplicated cases here would have meant two places to update for one behaviour, and the duplicate would
/// have been the less thorough of the two.
/// </para>
/// </summary>
public sealed class IReaderContractTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // Strings.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadSizedString_ReadsTheDeclaredByteCountAndThenAligns()
    {
        // length = 3, then "abc", then one pad byte so the next field starts on a 4-byte boundary.
        var reader = (IReader)new MemoryReader(new byte[]
        {
            0x03, 0x00, 0x00, 0x00,
            (byte)'a', (byte)'b', (byte)'c', 0x00,
            (byte)0xAA, 0x00, 0x00, 0x00
        })
        { Endian = Endianness.LittleEndian };

        Assert.Equal("abc", reader.ReadSizedString());
        Assert.Equal(8, reader.Position);
        // The align leaves the cursor on the next field, not inside the string.
        Assert.Equal(0xAAu, reader.ReadUInt32());
    }

    [Fact]
    public void ReadSizedString_InvalidLength_Throws()
    {
        var reader = (IReader)new MemoryReader(new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x01, 0x02 })
            { Endian = Endianness.LittleEndian };

        Assert.Throws<EndOfStreamException>(() => reader.ReadSizedString());
    }

    [Fact]
    public void ReadNullTerminatedString_StopsAtTheTerminatorAndConsumesIt()
    {
        var reader = new MemoryReader(new byte[] { (byte)'a', (byte)'b', 0x00, (byte)'z' });

        Assert.Equal("ab", reader.ReadNullTerminatedString());
        Assert.Equal(3, reader.Position);
        Assert.Equal((byte)'z', reader.ReadByte());
    }

    // A missing terminator is covered across all implementations in ReadStringContractTests, where the contract that is
    // asserted is "it is reported" rather than one particular exception type.

    // ---------------------------------------------------------------------------------------------------------------
    // Collections: the count lives in the stream, and the element width is entirely up to the caller.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadArray_ConsumesTheCountPrefixAndEveryElement()
    {
        var reader = (IReader)new MemoryReader(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x0A, 0x0B, 0x0C })
            { Endian = Endianness.LittleEndian };

        var values = reader.ReadArray(r => r.ReadByte());

        Assert.Equal(new byte[] { 0x0A, 0x0B, 0x0C }, values);
        Assert.Equal(7, reader.Position);
    }

    [Fact]
    public void ReadFixedArray_DoesNotConsumeACountPrefix()
    {
        var reader = (IReader)new MemoryReader(new byte[] { 0x0A, 0x0B, 0x0C });
        var values = new byte[3];

        reader.ReadFixedArray(values, r => r.ReadByte());

        Assert.Equal(new byte[] { 0x0A, 0x0B, 0x0C }, values);
        Assert.Equal(3, reader.Position);
    }

    [Fact]
    public void ReadArrayWithAlign_AlignsAfterEveryElementWhenAsked()
    {
        // Two one-byte elements, each followed by three pad bytes.
        var reader = (IReader)new MemoryReader(new byte[]
        {
            0x02, 0x00, 0x00, 0x00,
            0x0A, 0x00, 0x00, 0x00,
            0x0B, 0x00, 0x00, 0x00
        })
        { Endian = Endianness.LittleEndian };

        var values = reader.ReadArrayWithAlign(r => r.ReadByte(), requiresAlign: true);

        Assert.Equal(new byte[] { 0x0A, 0x0B }, values);
        Assert.Equal(12, reader.Position);
    }

    [Fact]
    public void ReadList_ConsumesTheCountPrefixAndEveryElement()
    {
        var reader = (IReader)new MemoryReader(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x0A, 0x0B })
            { Endian = Endianness.LittleEndian };

        var values = reader.ReadList(r => r.ReadByte());

        Assert.Equal(new byte[] { 0x0A, 0x0B }, values);
        Assert.Equal(6, reader.Position);
    }

    [Fact]
    public void ReadListWithNegativeCount_ThrowsAndConsumesNothing()
    {
        // The count is read from the file, so a negative one is a damaged file rather than a caller mistake; it has to be
        // refused instead of reaching `new List<T>(-1)`, which would throw something far less informative.
        var reader = (IReader)new MemoryReader(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0A })
            { Endian = Endianness.LittleEndian };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadList(r => r.ReadByte()));

        Assert.Equal("count", exception.ParamName);
    }

    [Fact]
    public void ReadPairWithAlign_AlignsAfterTheKeyBeforeReadingTheValue()
    {
        // A one-byte key, then padding up to the 4-byte boundary, then the value. `keyRequiresAlign` moves the cursor
        // before the value is read, so the fixture has to leave room for the jump — a four-byte buffer would put the
        // value past the end.
        var reader = (IReader)new MemoryReader(new byte[]
        {
            0x0A, 0x00, 0x00, 0x00,
            0x0B, 0x00, 0x00, 0x00
        })
        { Endian = Endianness.LittleEndian };

        var (key, value) = reader.ReadPairWithAlign(
            r => r.ReadByte(),
            r => r.ReadByte(),
            keyRequiresAlign: true,
            valueRequiresAlign: false);

        Assert.Equal((byte)0x0A, key);
        Assert.Equal((byte)0x0B, value);
        Assert.Equal(5, reader.Position);
    }

    /// <summary>The negative count prefixed as four bytes, so a test can spell the damaged input out.</summary>
    [Fact]
    public void ReadListWithZeroCount_ReturnsAnEmptyList()
    {
        var reader = (IReader)new MemoryReader(new byte[] { 0x00, 0x00, 0x00, 0x00, 0xAA })
            { Endian = Endianness.LittleEndian };

        var values = reader.ReadList(r => r.ReadByte());

        Assert.Empty(values);
        Assert.Equal(4, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Seek: the three origins, including the two arguments that are rejected. The matrices use Seek as setup, so the
    // member itself is only exercised here.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Seek_BeginAndCurrentAndEnd()
    {
        var reader = (IReader)new MemoryReader(Payload(16));

        reader.Seek(4);
        Assert.Equal(4, reader.Position);

        reader.Seek(3, SeekOrigin.Current);
        Assert.Equal(7, reader.Position);

        reader.Seek(-2, SeekOrigin.End);
        Assert.Equal(14, reader.Position);
    }

    [Fact]
    public void Seek_NegativeFromBegin_Throws()
    {
        var reader = (IReader)new MemoryReader(Payload(16));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Seek(-1, SeekOrigin.Begin));
    }

    [Fact]
    public void Seek_PositiveFromEnd_Throws()
    {
        var reader = (IReader)new MemoryReader(Payload(16));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Seek(1, SeekOrigin.End));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Align at the end of the data. The matrix covers the ordinary multiples; the interesting part is the boundary, and
    // it is a single-reader detail because the branch lives in the IReader default implementation rather than in any
    // particular reader.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Align_WhenTheStepIsAtLeastWhatIsLeft_ClampsToTheEnd()
    {
        // The branch that makes a truncated last entry readable: a bundle's final entry is written without padding, while
        // the reader still aligns after it. Position 3 of a 5-byte payload with alignment 8 needs a step of 5 and has 5
        // left, which is exactly the clamp. It must not consume anything in the middle of valid data.
        var reader = (IReader)new MemoryReader(Payload(5));

        reader.Seek(3);
        reader.Align(8);

        Assert.Equal(5, reader.Position);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Align_RepeatedFromAnAlignedPosition_IsANoOp()
    {
        // Repeating the call from an already aligned position must not move the cursor, which is what keeps the clamp from
        // eating data when a caller aligns more than once.
        var reader = (IReader)new MemoryReader(Payload(8));

        reader.Seek(3);
        reader.Align(4);
        Assert.Equal(4, reader.Position);

        reader.Align(4);
        reader.Align(4);
        Assert.Equal(4, reader.Position);

        // Payload is 0-based, so the byte at index 4 is 4 — and it is still there.
        Assert.Equal(4, reader.ReadByte());
    }

    [Fact]
    public void Align_FromTheEndOfTheData_StaysAtTheEnd()
    {
        // Aligning at the end has nothing to skip and must not move backwards or need a negative step.
        var reader = (IReader)new MemoryReader(Payload(8));
        reader.Seek(8);

        reader.Align(4);

        Assert.Equal(8, reader.Position);
        Assert.Equal(0, reader.Read(new byte[8], 0, 8));
    }

    [Fact]
    public void Position_PastTheEnd_IsRefusedSoAlignCannotStartFromThere()
    {
        // Align used to have a branch for a cursor past the end, because Position allowed that state. The setter refuses it
        // now, which is why that branch is only reachable from an in-range position.
        var reader = (IReader)new MemoryReader(Payload(8));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = 9);
    }

    private static byte[] Payload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)i;

        return payload;
    }
}