using System.Globalization;
using System.Text;
using UnityAsset.NET.IO;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The fixed-width readers, driven by one data table rather than one test per type: the shape is identical for all of
/// them — consume exactly the width, in the endianness <c>Endian</c> asks for — so what differs is only the bytes, the
/// expected value and the call. Adding <c>ReadInt128</c> should mean adding a row, not a test.
/// <para>
/// Every row runs against every implementation. The implementations are reached through the same switch, which is also
/// the place a missing implementation is turned into a loud failure instead of a silently skipped row.
/// </para>
/// </summary>
public sealed class TypedReadContractTests
{
    public enum TypedRead
    {
        Byte,
        SByte,
        Boolean,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
    }

    /// <summary>
    /// One typed read: the bytes of the value as they appear in the file (<paramref name="ValueBytes"/>), the width it
    /// must consume, and the value it must produce. <paramref name="Tail"/> is filler after the value, so the buffer is
    /// longer than the read and "consumed exactly the width" is observable.
    /// </summary>
    public sealed record TypedCase(
        TypedRead Read,
        string ValueBytes,
        int Width,
        string Tail = "",
        bool Integer = false,
        long ExpectedInteger = 0,
        double ExpectedReal = 0)
    {
        public override string ToString() => Read.ToString();

        /// <summary>The whole fixture: the value, then the filler.</summary>
        public byte[] Bytes => Payload(ValueBytes).Concat(Payload(Tail)).ToArray();
    }

    private static readonly TypedCase[] Cases =
    [
        new(TypedRead.Byte,    "AB", Tail: "CD EF", Width: 1, Integer: true, ExpectedInteger: 0xAB),
        new(TypedRead.SByte,   "FF", Tail: "00 00", Width: 1, Integer: true, ExpectedInteger: -1),
        new(TypedRead.Boolean, "00", Tail: "00 00", Width: 1, Integer: true, ExpectedInteger: 0),
        new(TypedRead.Boolean, "01", Tail: "00 00", Width: 1, Integer: true, ExpectedInteger: 1),
        new(TypedRead.Boolean, "FF", Tail: "00 00", Width: 1, Integer: true, ExpectedInteger: 1),   // any non-zero is true
        new(TypedRead.Int16,   "7F FF", Tail: "00", Width: 2, Integer: true, ExpectedInteger: 0x7FFF),
        new(TypedRead.UInt16,  "FF FE", Tail: "00", Width: 2, Integer: true, ExpectedInteger: 0xFFFE),
        new(TypedRead.Int32,   "7F FF FF FF", Width: 4, Integer: true, ExpectedInteger: int.MaxValue),
        new(TypedRead.Int32,   "80 00 00 00", Width: 4, Integer: true, ExpectedInteger: int.MinValue),
        new(TypedRead.UInt32,  "FF FF FF FE", Width: 4, Integer: true, ExpectedInteger: uint.MaxValue - 1),
        new(TypedRead.Int64,   "01 02 03 04 05 06 07 08", Width: 8, Integer: true, ExpectedInteger: 0x0102030405060708),
        new(TypedRead.UInt64,  "FF 00 00 00 00 00 00 01", Width: 8, Integer: true,
            ExpectedInteger: unchecked((long)0xFF00000000000001UL)),
        // 1.0f is 3F800000 and 1.0 is 3FF0000000000000.
        new(TypedRead.Single,  "3F 80 00 00", Width: 4, ExpectedReal: 1.0),
        new(TypedRead.Double,  "3F F0 00 00 00 00 00 00", Width: 8, ExpectedReal: 1.0),
    ];

    public static TheoryData<ReaderImpl, TypedCase> CasesData
    {
        get
        {
            var data = new TheoryData<ReaderImpl, TypedCase>();
            foreach (var impl in ReaderFactory.All)
            {
                foreach (var @case in Cases)
                    data.Add(impl, @case);
            }

            return data;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Big endian: one row per type, every implementation.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CasesData))]
    public void TypedRead_BigEndian_ConsumesExactlyItsWidth(ReaderImpl impl, TypedCase @case)
    {
        var bytes = @case.Bytes;
        var reader = impl.Create(bytes);
        reader.Endian = Endianness.BigEndian;

        var (integer, real) = Invoke(reader, @case.Read);

        AssertInteger(@case, integer);
        AssertReal(@case, real);
        Assert.Equal(@case.Width, reader.Position);
        Assert.Equal(bytes.Length - @case.Width, ((IReader)reader).Remaining);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Little endian: the same call, the bytes read backwards.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CasesData))]
    public void TypedRead_LittleEndian_ReadsTheSameValueFromReversedBytes(ReaderImpl impl, TypedCase @case)
    {
        // Only the value's own bytes are reversed; the filler after it stays where it is, so the fixture still has a tail
        // to prove the read stopped at its width.
        var valueBytes = Payload(@case.ValueBytes).Reverse().ToArray();
        var bytes = valueBytes.Concat(Payload(@case.Tail)).ToArray();
        var reader = impl.Create(bytes);
        reader.Endian = Endianness.LittleEndian;

        var (integer, real) = Invoke(reader, @case.Read);

        AssertInteger(@case, integer);
        AssertReal(@case, real);
        Assert.Equal(@case.Width, reader.Position);
    }

    [Fact]
    public void TypedRead_Single_HonoursEndianness()
    {
        // Spelled out once, because the pair of byte sequences is the clearest statement of what Endian means.
        var big = ReaderFactory.MemoryReader.Create(Payload("3F 80 00 00"));
        big.Endian = Endianness.BigEndian;
        Assert.Equal(1.0f, big.ReadSingle());

        var little = ReaderFactory.MemoryReader.Create(Payload("00 00 80 3F"));
        little.Endian = Endianness.LittleEndian;
        Assert.Equal(1.0f, little.ReadSingle());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The family is built on ReadByte, so it has to answer the same way at end of data. Only "it throws" is asserted:
    // the exception type is one of the differences the review records, and pinning a different type per implementation
    // here would hide that.
    // ---------------------------------------------------------------------------------------------------------------

    public static TheoryData<ReaderImpl, TypedRead> AtEndData
    {
        get
        {
            var data = new TheoryData<ReaderImpl, TypedRead>();
            foreach (var impl in ReaderFactory.All)
            {
                foreach (var read in Enum.GetValues<TypedRead>())
                    data.Add(impl, read);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AtEndData))]
    public void TypedRead_AtTheEndOfTheData_ThrowsAndConsumesNothing(ReaderImpl impl, TypedRead read)
    {
        var reader = impl.Create(ReaderFactory.Payload(8));
        ((IReader)reader).Seek(8);

        Assert.NotNull(Record.Exception(() => Invoke(reader, read)));
        Assert.Equal(8, reader.Position);
    }

    [Fact]
    public void TypedRead_OneByteShortOfItsWidth_Throws()
    {
        // A 4-byte read with 3 bytes left: the failure has to happen rather than quietly reading into nothing.
        var reader = ReaderFactory.MemoryReader.Create(ReaderFactory.Payload(8));
        ((IReader)reader).Seek(5);

        Assert.NotNull(Record.Exception(() => reader.ReadInt32()));
        Assert.Equal(5, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Reading past a window boundary: the buffered implementations take a slow path here, and it has to agree with the
    // in-memory one byte for byte.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CasesData))]
    public void TypedRead_StraddlingTheReadWindow_ProducesTheSameValue(ReaderImpl impl, TypedCase @case)
    {
        if (@case.Width == 1)
            return;   // a single byte cannot straddle anything

        // Two bytes of filler plus the value, positioned so the value crosses a 16-byte window boundary.
        var filler = ReaderFactory.Payload(14);
        var bytes = filler.Concat(@case.Bytes).ToArray();
        var reader = impl.Create(bytes);
        reader.Endian = Endianness.BigEndian;
        ((IReader)reader).Seek(14);

        var (integer, real) = Invoke(reader, @case.Read);

        AssertInteger(@case, integer);
        AssertReal(@case, real);
        Assert.Equal(14 + @case.Width, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The one place the implementations are dispatched. A factory that does not know an implementation would otherwise
    /// leave every row silently untested, so an unknown one throws.
    /// </summary>
    private static (long Integer, double Real) Invoke(IReader reader, TypedRead read) => read switch
    {
        TypedRead.Byte => (reader.ReadByte(), 0),
        TypedRead.SByte => (reader.ReadSByte(), 0),
        TypedRead.Boolean => (reader.ReadBoolean() ? 1 : 0, 0),
        TypedRead.Int16 => (reader.ReadInt16(), 0),
        TypedRead.UInt16 => (reader.ReadUInt16(), 0),
        TypedRead.Int32 => (reader.ReadInt32(), 0),
        TypedRead.UInt32 => (reader.ReadUInt32(), 0),
        TypedRead.Int64 => (reader.ReadInt64(), 0),
        TypedRead.UInt64 => (unchecked((long)reader.ReadUInt64()), 0),
        TypedRead.Single => (0, reader.ReadSingle()),
        TypedRead.Double => (0, reader.ReadDouble()),
        _ => throw new ArgumentOutOfRangeException(nameof(read), read, "Unknown typed read."),
    };

    private static void AssertInteger(TypedCase @case, long actual)
    {
        if (@case.Integer)
            Assert.Equal(@case.ExpectedInteger, actual);
    }

    private static void AssertReal(TypedCase @case, double actual)
    {
        if (!@case.Integer)
            Assert.Equal(@case.ExpectedReal, actual, precision: 6);
    }

    /// <summary>"7F FF" as a byte array, in file order.</summary>
    private static byte[] Payload(string hex)
        => hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => byte.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToArray();

    /// <summary>The bytes of a string, used by the string tests below.</summary>
    private static byte[] Ascii(string value) => Encoding.UTF8.GetBytes(value);
}