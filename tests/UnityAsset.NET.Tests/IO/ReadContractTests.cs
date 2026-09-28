using UnityAsset.NET.IO;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The <c>Read</c> family as a matrix: every case is a named row, and every row runs against every implementation.
/// <para>
/// The cross product is built in the data, not in a loop inside the test, so each combination is its own test case: a
/// failure names the implementation and the case instead of hiding behind "the matrix". That also satisfies xUnit's
/// requirement that <c>TheoryData</c> carries exactly the arguments the test method takes.
/// </para>
/// <para>
/// Three questions are load-bearing, and each is its own case kind rather than one folded assertion: how much a read
/// consumes, what it does when the source runs out, and what it does when the arguments cannot work.
/// </para>
/// </summary>
public sealed class ReadContractTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // A read that succeeds: the count is what gets consumed, the bytes land at `offset`, nothing else is touched.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record ReadCase(string Name, int Source, int Target, int Offset, int Count, int Expected)
    {
        public override string ToString() => Name;
    }

    private static readonly ReadCase[] Reads =
    [
        new("basic-4", Source: 64, Target: 64, Offset: 0, Count: 4, Expected: 4),
        new("basic-8", Source: 64, Target: 64, Offset: 0, Count: 8, Expected: 8),
        new("offset-4", Source: 64, Target: 64, Offset: 4, Count: 8, Expected: 8),
        // To the very end of the target: the largest request that is still legal.
        new("to-target-end", Source: 64, Target: 64, Offset: 12, Count: 52, Expected: 52),
        new("last-4", Source: 64, Target: 64, Offset: 60, Count: 4, Expected: 4),
        // A target far larger than the request: the reader may not fill it.
        new("small-request-big-target", Source: 64, Target: 256, Offset: 0, Count: 4, Expected: 4),
        new("late-offset-big-target", Source: 64, Target: 256, Offset: 100, Count: 8, Expected: 8),
        // The source runs out before the request does: a short read, not an error.
        new("short-by-54", Source: 10, Target: 64, Offset: 0, Count: 64, Expected: 10),
        new("short-by-1", Source: 10, Target: 64, Offset: 0, Count: 11, Expected: 10),
        // Nothing asked for.
        new("zero-count", Source: 64, Target: 64, Offset: 0, Count: 0, Expected: 0),
    ];

    public static TheoryData<ReaderImpl, ReadCase> ReadsData => Cross(Reads);

    [Theory]
    [MemberData(nameof(ReadsData))]
    public void Read_ConsumesWhatTheCountAllows_AndLandsItAtTheOffset(ReaderImpl impl, ReadCase @case)
    {
        var payload = ReaderFactory.Payload(@case.Source);
        var reader = impl.Create(payload);
        var buffer = new byte[@case.Target];
        buffer.AsSpan().Fill(0xFF);

        var read = reader.Read(buffer, @case.Offset, @case.Count);

        Assert.Equal(@case.Expected, read);
        Assert.Equal(@case.Expected, reader.Position);
        Assert.Equal(@case.Expected, payload.Length - ((IReader)reader).Remaining);
        Assert.Equal(payload.Take(@case.Expected), buffer.Skip(@case.Offset).Take(@case.Expected));
        // Everything outside the written span is untouched: before the offset, and after what was written.
        Assert.All(buffer.Take(@case.Offset), b => Assert.Equal(0xFF, b));
        Assert.All(buffer.Skip(@case.Offset + @case.Expected), b => Assert.Equal(0xFF, b));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // End of source: a short read of zero, never an exception. This is the contract ReadExactly builds on.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record ExhaustedCase(string Name, int Source, int Count)
    {
        public override string ToString() => Name;
    }

    private static readonly ExhaustedCase[] Exhausted =
    [
        new("ask-past-end", Source: 64, Count: 8),
        new("ask-full-window", Source: 64, Count: 64),
        new("source-below-window", Source: 8, Count: 4),
        new("source-below-window-ask-more", Source: 8, Count: 64),
    ];

    public static TheoryData<ReaderImpl, ExhaustedCase> ExhaustedData => Cross(Exhausted);

    [Theory]
    [MemberData(nameof(ExhaustedData))]
    public void Read_AtTheEndOfTheData_ReturnsZero(ReaderImpl impl, ExhaustedCase @case)
    {
        // The source is consumed by plain reads, so this case does not depend on the member it is meant to be
        // independent of.
        var reader = impl.Create(ReaderFactory.Payload(@case.Source));
        Assert.Equal(@case.Source, reader.Read(new byte[@case.Source], 0, @case.Source));

        var buffer = new byte[64];
        buffer.AsSpan().Fill(0xFF);

        Assert.Equal(0, reader.Read(buffer, 0, @case.Count));
        Assert.All(buffer, b => Assert.Equal(0xFF, b));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Read_WithTheCursorAtTheEnd_ReturnsZero(ReaderImpl impl)
    {
        // The furthest the cursor can legitimately reach, which is where a completed read leaves it.
        var reader = impl.Create(ReaderFactory.Payload(16));
        ((IReader)reader).Seek(16);

        Assert.Equal(16, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);
        Assert.Equal(0, reader.Read(new byte[16], 0, 16));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Position_PastTheEnd_IsRefused(ReaderImpl impl)
    {
        // A cursor past the data is not a state a reader should be able to enter: Remaining would go negative and every
        // loop on it would read differently. It is refused at the assignment instead.
        var reader = impl.Create(ReaderFactory.Payload(16));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = 17);

        Assert.Equal("value", exception.ParamName);
        Assert.Equal(0, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Position_ThatTheInternalStorageCannotHold_IsRefused(ReaderImpl impl)
    {
        // Position is a long on the interface while MemoryReader keeps an int, so a value the storage cannot represent has
        // to be refused by the range check rather than truncated into a cursor nobody asked for.
        var reader = impl.Create(ReaderFactory.Payload(16));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = int.MaxValue + 1L);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = long.MaxValue);
        Assert.Equal(0, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Position_Negative_IsRefused(ReaderImpl impl)
    {
        var reader = impl.Create(ReaderFactory.Payload(16));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = -1);
        Assert.Equal(0, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Arguments that cannot work: the one case that throws, and it has to name the parameter that was wrong.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record BadArgumentCase(string Name, int Offset, int Count, string ExpectedParamName)
    {
        public override string ToString() => Name;
    }

    private static readonly BadArgumentCase[] BadArguments =
    [
        new("offset-negative", Offset: -1, Count: 4, ExpectedParamName: "offset"),
        new("offset-past-target", Offset: 9, Count: 0, ExpectedParamName: "offset"),
        new("offset-max", Offset: int.MaxValue, Count: 0, ExpectedParamName: "offset"),
        new("count-negative", Offset: 0, Count: -1, ExpectedParamName: "count"),
        new("count-very-negative", Offset: 4, Count: -8, ExpectedParamName: "count"),
        new("overrun-by-1", Offset: 8, Count: 1, ExpectedParamName: "count"),
        new("overrun-mid", Offset: 4, Count: 5, ExpectedParamName: "count"),
        new("count-past-target", Offset: 0, Count: 9, ExpectedParamName: "count"),
    ];

    public static TheoryData<ReaderImpl, BadArgumentCase> BadArgumentsData => Cross(BadArguments);

    [Theory]
    [MemberData(nameof(BadArgumentsData))]
    public void Read_WithArgumentsThatCannotFitTheTarget_ThrowsAndNamesTheParameter(
        ReaderImpl impl, BadArgumentCase @case)
    {
        var reader = impl.Create(ReaderFactory.Payload(64));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => reader.Read(new byte[8], @case.Offset, @case.Count));

        Assert.Equal(@case.ExpectedParamName, exception.ParamName);
        // Rejected before anything was consumed, so a caller that catches the error does not find its reader advanced.
        Assert.Equal(0, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // ReadExactly: the one member whose behaviour is identical across the implementations.
    // ---------------------------------------------------------------------------------------------------------------

    public sealed record ExactlyCase(string Name, int Source, int Consume, int Request)
    {
        public override string ToString() => Name;
    }

    private static readonly ExactlyCase[] Exactly =
    [
        new("whole-source", Source: 64, Consume: 0, Request: 64),
        new("from-offset", Source: 64, Consume: 8, Request: 16),
        new("tail", Source: 64, Consume: 60, Request: 4),
        new("below-window", Source: 8, Consume: 0, Request: 8),
        new("zero-request", Source: 64, Consume: 0, Request: 0),
    ];

    public static TheoryData<ReaderImpl, ExactlyCase> ExactlyData => Cross(Exactly);

    [Theory]
    [MemberData(nameof(ExactlyData))]
    public void ReadExactly_WhenTheDataIsThere_ConsumesExactlyThatMuch(ReaderImpl impl, ExactlyCase @case)
    {
        var payload = ReaderFactory.Payload(@case.Source);
        var reader = impl.Create(payload);
        if (@case.Consume > 0)
            reader.ReadExactly(new byte[@case.Consume]);

        var buffer = new byte[@case.Request];
        reader.ReadExactly(buffer);

        Assert.Equal(@case.Consume + @case.Request, reader.Position);
        Assert.Equal(payload.Skip(@case.Consume).Take(@case.Request), buffer);
    }

    public sealed record ShortExactlyCase(string Name, int Source, int Consume, int Request)
    {
        public override string ToString() => Name;
    }

    private static readonly ShortExactlyCase[] ShortExactly =
    [
        new("one-byte-short", Source: 64, Consume: 63, Request: 4),
        new("at-the-end", Source: 64, Consume: 64, Request: 1),
        new("source-below-window", Source: 8, Consume: 0, Request: 9),
    ];

    public static TheoryData<ReaderImpl, ShortExactlyCase> ShortExactlyData => Cross(ShortExactly);

    [Theory]
    [MemberData(nameof(ShortExactlyData))]
    public void ReadExactly_WhenTheDataIsShort_ThrowsEndOfStream(ReaderImpl impl, ShortExactlyCase @case)
    {
        var reader = impl.Create(ReaderFactory.Payload(@case.Source));
        if (@case.Consume > 0)
            ((IReader)reader).Seek(@case.Consume);

        Assert.Throws<EndOfStreamException>(() => reader.ReadExactly(new byte[@case.Request]));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadExactly_OnAnEmptySource_ThrowsEndOfStream(ReaderImpl impl)
    {
        var reader = impl.Create([]);

        Assert.Throws<EndOfStreamException>(() => reader.ReadExactly(new byte[1]));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadExactly_WithAZeroLengthBuffer_DoesNothing(ReaderImpl impl)
    {
        var reader = impl.Create(ReaderFactory.Payload(8));

        reader.ReadExactly([]);

        Assert.Equal(0, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // ReadBytes: the array is allocated by the implementation, so how it answers a shortage is its own question.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadBytes_WhenTheDataIsThere_ReturnsExactlyThatManyBytes(ReaderImpl impl)
    {
        var payload = ReaderFactory.Payload(32);
        var reader = impl.Create(payload);

        Assert.Equal(payload.Take(8), reader.ReadBytes(8));
        Assert.Equal(8, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadBytes_AcrossTheReadWindow_ReturnsTheWholeRun(ReaderImpl impl)
    {
        // 40 bytes through a 16-byte window: the run crosses refills, so the array has to be assembled from more than one
        // window rather than truncated at the first boundary.
        var payload = ReaderFactory.Payload(40);
        var reader = impl.Create(payload);

        Assert.Equal(payload, reader.ReadBytes(40));
        Assert.Equal(40, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadBytes_AtTheEndOfTheData_Throws(ReaderImpl impl)
    {
        // A shortage is an error here, unlike Read, which reports it as a short read — and each implementation answers
        // with the type it happens to reach. The type is asserted exactly, per implementation, because that difference is
        // a decision someone has to make deliberately: pinning only "it throws" would let a change slip through.
        var reader = impl.Create(ReaderFactory.Payload(4));
        reader.ReadBytes(4);

        var expected = ExpectedShortageType(impl);
        var exception = Record.Exception(() => reader.ReadBytes(4));

        Assert.NotNull(exception);
        Assert.IsType(expected, exception);
        // A refused read leaves the cursor where it was.
        Assert.Equal(4, reader.Position);
    }

    /// <summary>
    /// The exception each implementation raises for "there is not enough data". MemoryReader reaches the span slice and
    /// reports ArgumentOutOfRangeException; the others route through ReadExactly and report EndOfStreamException. The
    /// review records the unification; this switch is the place that would change with it.
    /// </summary>
    private static Type ExpectedShortageType(ReaderImpl impl) => impl.Name switch
    {
        "MemoryReader" => typeof(ArgumentOutOfRangeException),
        "CustomFileReader" => typeof(EndOfStreamException),
        "SlicedReader" => typeof(Exception),
        "BlockReader" => typeof(EndOfStreamException),
        _ => throw new ArgumentOutOfRangeException(nameof(impl), impl.Name, "Unknown reader implementation."),
    };

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadBytes_WithANegativeCount_ThrowsAndConsumesNothing(ReaderImpl impl)
    {
        var reader = impl.Create(ReaderFactory.Payload(16));

        Assert.NotNull(Record.Exception(() => reader.ReadBytes(-1)));
        Assert.Equal(0, reader.Position);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void ReadBytes_WithZeroCount_ReturnsAnEmptyArray(ReaderImpl impl)
    {
        var reader = impl.Create(ReaderFactory.Payload(16));

        Assert.Empty(reader.ReadBytes(0));
        Assert.Equal(0, reader.Position);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------------------------

    public static TheoryData<ReaderImpl> Implementations => ReaderFactory.AllData;

    private static TheoryData<ReaderImpl, TCase> Cross<TCase>(TCase[] cases)
    {
        var data = new TheoryData<ReaderImpl, TCase>();
        foreach (var impl in ReaderFactory.All)
        {
            foreach (var @case in cases)
                data.Add(impl, @case);
        }

        return data;
    }
}