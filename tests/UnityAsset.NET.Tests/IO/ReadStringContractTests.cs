using System.Globalization;
using System.Text;
using UnityAsset.NET.IO;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The string readers, across every implementation and both read shapes.
/// <para>
/// The exception <em>type</em> each implementation raises is its own choice, so these cases assert the contract instead:
/// a malformed or terminated-elsewhere string has to be reported rather than silently turned into an empty value, and
/// nothing may be consumed when the read is refused. What the members must agree on is where the cursor ends up on a
/// successful read — that is what makes a file parseable at all.
/// </para>
/// </summary>
public sealed class ReadStringContractTests
{
    public static TheoryData<ReaderImpl> Implementations => ReaderFactory.AllData;

    // ---------------------------------------------------------------------------------------------------------------
    // Null-terminated strings.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record TerminatedCase(string Name, byte[] Payload, string Expected, int ExpectedPosition)
    {
        public override string ToString() => Name;
    }

    private static readonly TerminatedCase[] Terminated =
    [
        new("ascii", Ascii("ab\0cd\0"), "ab", 3),
        new("empty", Ascii("\0rest"), "", 1),
        new("multi-byte-utf8", Utf8("你好\0tail"), "你好", 7),     // 3 + 3 bytes and the terminator
        new("four-byte-utf8", Utf8("😀\0tail"), "😀", 5),          // 4 bytes and the terminator
        new("terminator-is-the-last-byte", Ascii("abc\0"), "abc", 4),
    ];

    public static TheoryData<ReaderImpl, TerminatedCase> TerminatedData
    {
        get
        {
            var data = new TheoryData<ReaderImpl, TerminatedCase>();
            foreach (var impl in ReaderFactory.All)
            {
                foreach (var @case in Terminated)
                    data.Add(impl, @case);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TerminatedData))]
    public void ReadNullTerminatedString_ReadsToTheTerminatorAndConsumesIt(ReaderImpl impl, TerminatedCase @case)
    {
        var reader = impl.Create(@case.Payload);

        var value = reader.ReadNullTerminatedString();

        Assert.Equal(@case.Expected, value);
        Assert.Equal(@case.ExpectedPosition, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadNullTerminatedString_WithoutATerminator_ThrowsAndKeepsTheCursorAtTheStart(ReaderImpl impl)
    {
        // Which exception type is raised is each implementation's own decision, so only the contract is asserted: it has
        // to be reported rather than silently turned into a short string. MemoryReader and SlicedReader find out before
        // consuming anything; CustomFileReader and BlockReader discover it at the end of the data and have consumed the
        // whole run by then. The cursor values below are therefore per implementation, and the number is the evidence:
        // a caller cannot rely on the position being untouched after a refused read.
        var reader = impl.Create([1, 2, 3, 4, 5, 6]);

        var exception = Record.Exception(() => reader.ReadNullTerminatedString());

        Assert.NotNull(exception);

        var expected = impl.Name is "MemoryReader" or "SlicedReader" ? 0 : 6;
        Assert.Equal(expected, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadNullTerminatedString_AtTheEndOfTheData_Throws(ReaderImpl impl)
    {
        var reader = impl.Create(ReaderFactory.Payload(8));
        ((IReader)reader).Seek(8);

        Assert.NotNull(Record.Exception(() => reader.ReadNullTerminatedString()));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Size-prefixed strings: the prefix counts bytes, and the field is padded to four.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record SizedCase(string Name, byte[] Payload, string Expected, int ExpectedPosition)
    {
        public override string ToString() => Name;
    }

    private static readonly SizedCase[] Sized =
    [
        // Four-byte prefix, three ASCII bytes, one pad byte.
        new("ascii", Concat(Int32(3), Ascii("abc"), [0x00]), "abc", 8),
        new("ascii-with-tail", Concat(Int32(2), Ascii("ab"), [0x00, 0x00], [0xAA]), "ab", 8),
        // Six UTF-8 bytes for two characters, then two pad bytes.
        new("multi-byte-utf8", Concat(Int32(6), Utf8("你好"), [0x00, 0x00]), "你好", 12),
    ];

    public static TheoryData<ReaderImpl, SizedCase> SizedData
    {
        get
        {
            var data = new TheoryData<ReaderImpl, SizedCase>();
            foreach (var impl in ReaderFactory.All)
            {
                foreach (var @case in Sized)
                    data.Add(impl, @case);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SizedData))]
    public void ReadSizedString_ReadsTheDeclaredByteCountAndAligns(ReaderImpl impl, SizedCase @case)
    {
        var reader = impl.Create(@case.Payload);
        reader.Endian = Endianness.LittleEndian;

        var value = ((IReader)reader).ReadSizedString();

        Assert.Equal(@case.Expected, value);
        Assert.Equal(@case.ExpectedPosition, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadSizedString_PrefixLongerThanTheData_IsReportedNotSwallowed(ReaderImpl impl)
    {
        // The prefix claims 64 bytes and the reader holds four. Returning an empty string here — which IReader.ReadSizedString
        // used to do — makes a corrupt length read as an empty field while the cursor still moves past the prefix, so every
        // field after it is read from the wrong place. The read is reported now; the cursor is four bytes along, which is
        // worth stating out loud: the prefix itself is already consumed when the shortage is discovered.
        var reader = impl.Create(Concat(Int32(64), [(byte)'a', (byte)'b', (byte)'c', (byte)'d']));
        reader.Endian = Endianness.LittleEndian;

        var exception = Record.Exception(() => ((IReader)reader).ReadSizedString());

        Assert.NotNull(exception);
        Assert.Equal(4, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadSizedString_WithAPositivePrefixButNoBody_IsReportedNotSwallowed(ReaderImpl impl)
    {
        // A prefix of 1 with nothing after it: the reader holds exactly the prefix.
        var reader = impl.Create(Int32(1));
        reader.Endian = Endianness.LittleEndian;

        Assert.NotNull(Record.Exception(() => ((IReader)reader).ReadSizedString()));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadSizedString_WithANegativePrefix_IsReportedNotSwallowed(ReaderImpl impl)
    {
        // A negative prefix used to take the same "return empty" path as an over-long one.
        var reader = impl.Create(Concat(Int32(-1), [(byte)'a', (byte)'b', (byte)'c', (byte)'d']));
        reader.Endian = Endianness.LittleEndian;

        Assert.NotNull(Record.Exception(() => ((IReader)reader).ReadSizedString()));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadSizedString_WithAZeroPrefix_ReadsAnEmptyStringAndAligns(ReaderImpl impl)
    {
        // A zero-length string is legal data rather than a damaged prefix, so it has to read as an empty string and still
        // leave the cursor on the next 4-byte boundary.
        var reader = impl.Create(Concat(Int32(0), [(byte)0xAA, 0x00, 0x00, 0x00, 0xBB]));
        reader.Endian = Endianness.LittleEndian;

        Assert.Equal(string.Empty, ((IReader)reader).ReadSizedString());
        Assert.Equal(4, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // char: two bytes, and deliberately not controlled by Endian.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadChar_IsTwoBytesAndIgnoresEndian(ReaderImpl impl)
    {
        // BitConverter.ToChar follows the host byte order rather than Endian, which is by design — the member is used for
        // the type tree's own `char`, not for file data whose byte order matters. On this host (x64, little endian)
        // {41 00} is 'A'; {00 41} is the same two bytes in the other order and therefore 0x4100. Pinpointed so that a
        // future change to honour Endian is a deliberate one.
        var littleEndianHost = BitConverter.IsLittleEndian;

        var first = impl.Create([0x41, 0x00, 0x00]);
        first.Endian = Endianness.BigEndian;
        Assert.Equal(littleEndianHost ? 'A' : (char)0x4100, first.ReadChar());

        var second = impl.Create([0x41, 0x00, 0x00]);
        second.Endian = Endianness.LittleEndian;
        Assert.Equal(littleEndianHost ? 'A' : (char)0x4100, second.ReadChar());

        // Both reads consumed exactly two bytes.
        Assert.Equal(2, first.Position);
        Assert.Equal(2, second.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------------------------

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static byte[] Int32(int value) => BitConverter.GetBytes(value);

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}