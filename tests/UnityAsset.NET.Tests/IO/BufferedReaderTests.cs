using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.Tests.Support;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The buffered file reader (<see cref="CustomFileReader"/>) and the memory reader (<see cref="MemoryReader"/>).
/// <para>
/// <see cref="CustomFileReader"/> is the one with real state to get wrong: it keeps a window of the file plus a cursor
/// inside that window, so "where am I" is the sum of two moving parts. The cases below pin the window boundaries, the
/// refill on demand, a <c>Position</c> that lands inside a full window, and a file that is shorter than it claims.
/// </para>
/// </summary>
public sealed class BufferedReaderTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // CustomFileReader: the cursor arithmetic across refills.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Read_SpansSeveralRefills_AndReturnsTheBytesInOrder()
    {
        // 100 bytes with an 8-byte window: every read crosses a refill.
        var payload = Payload(100);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 8);
        var buffer = new byte[100];

        var read = reader.Read(buffer, 0, buffer.Length);

        Assert.Equal(100, read);
        Assert.Equal(payload, buffer);
        Assert.Equal(100, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);
    }

    [Fact]
    public void Read_ByteAtATime_KeepsPositionAndValuesExact()
    {
        var payload = Payload(20);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 8);

        for (var i = 0; i < payload.Length; i++)
        {
            Assert.Equal(i, reader.Position);
            Assert.Equal(payload[i], reader.ReadByte());
        }

        Assert.Equal(payload.Length, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);
    }

    [Fact]
    public void Read_AtTheEnd_ReturnsZero()
    {
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", Payload(8)), bufferSize: 8);
        var buffer = new byte[8];

        Assert.Equal(8, reader.Read(buffer, 0, 8));
        Assert.Equal(0, reader.Read(buffer, 0, 8));
    }

    [Fact]
    public void Read_EndsExactlyAtTheEndOfTheFile_WithoutOverconsuming()
    {
        // The loop stops on Remaining, not on a short read, so the last read has to stop one byte before the end.
        var payload = Payload(17);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 8);
        var buffer = new byte[64];

        var read = reader.Read(buffer, 0, buffer.Length);

        Assert.Equal(17, read);
        Assert.Equal(payload, buffer.Take(17));
        Assert.Equal(17, reader.Position);
    }

    [Fact]
    public void Read_ARefillMayOverFetchIntoTheWindow_ButTheCursorAdvancesByTheRequestOnly()
    {
        // The first read with an empty window fills the whole window (8 bytes here) to satisfy a 4-byte request. That
        // is the normal buffered-reader trade and it is safe: the extra bytes stay inside the reader's private window
        // and are not lost. What must hold is that the logical cursor, and therefore the next read, only advance by
        // what the caller asked for — an over-fetch that also moved the cursor is how offsets silently drift.
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", Payload(1024)), bufferSize: 8);
        var buffer = new byte[8];

        Assert.Equal(4, reader.Read(buffer, 0, 4));
        Assert.Equal(4, reader.Position);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, buffer.Take(4));

        // The bytes that were pulled in early are still there, in order.
        Assert.Equal(4, reader.Read(buffer, 0, 4));
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, buffer.Take(4));
        Assert.Equal(8, reader.Position);
    }

    [Fact]
    public void Position_SetForwardInsideTheWindow_DoesNotRefill()
    {
        var payload = Payload(64);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 64);
        reader.ReadByte(); // fill the window once

        reader.Position = 10;

        Assert.Equal(10, reader.Position);
        Assert.Equal(payload[10], reader.ReadByte());
        Assert.Equal(11, reader.Position);
    }

    [Fact]
    public void Position_SetBackwards_IsStillCorrect()
    {
        var payload = Payload(64);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 64);

        reader.Position = 32;
        Assert.Equal(payload[32], reader.ReadByte());

        reader.Position = 4;
        Assert.Equal(4, reader.Position);
        Assert.Equal(payload[4], reader.ReadByte());
    }

    [Fact]
    public void Position_SetPastTheEnd_IsRefused()
    {
        // A cursor past the file is not a state the reader allows: Remaining would go negative and every loop on it would
        // behave differently. The assignment is refused before anything is touched.
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", Payload(8)), bufferSize: 8);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = 64);

        Assert.Equal("value", exception.ParamName);
        Assert.Equal(0, reader.Position);
        Assert.Equal(8, ((IReader)reader).Remaining);
    }

    [Fact]
    public void Position_SetToTheEnd_IsAcceptedAndReadsNothing()
    {
        // The furthest legitimate cursor: where a completed read leaves it.
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", Payload(8)), bufferSize: 8);
        reader.ReadBytes(8);

        Assert.Equal(8, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);

        var buffer = new byte[8];
        buffer.AsSpan().Fill(0xFF);
        Assert.Equal(0, reader.Read(buffer, 0, 8));
        Assert.All(buffer, b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void TypedReads_AtAWindowBoundary_ReadTheSameBytesAsAPlainSlice()
    {
        // Reading an int when only two bytes are left in the window forces the slow path (a stack buffer plus
        // ReadExactly). It has to produce exactly what a contiguous read would.
        var payload = Payload(64);
        var expected = new MemoryReader(payload) { Endian = Endianness.LittleEndian };
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 6)
            { Endian = Endianness.LittleEndian };

        reader.ReadBytes(4);          // window: bytes 0..5, two left
        expected.ReadBytes(4);

        Assert.Equal(expected.ReadInt32(), reader.ReadInt32());
        Assert.Equal(expected.Position, reader.Position);
    }

    [Fact]
    public void ReadNullTerminatedString_AcrossAWindowBoundary()
    {
        // Nineteen 0x01 bytes and a terminator at the very end, read through a 4-byte window: the scan has to refill
        // four times and must not stop early, and it must stop exactly on the terminator.
        var payload = Enumerable.Repeat((byte)0x01, 19).Append((byte)0x00).ToArray();
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 4);

        Assert.Equal(new string('\u0001', 19), reader.ReadNullTerminatedString());
        Assert.Equal(20, reader.Position);
    }

    [Fact]
    public void ReadNullTerminatedString_WithoutATerminator_ThrowsEndOfStream()
    {
        // No zero byte anywhere, so the scan runs into the end of the file and FillBuffer reports it.
        var reader = new CustomFileReader(
            IoFiles.Raw("payload.bin", new byte[] { 1, 2, 3, 4, 5, 6 }), bufferSize: 4);

        Assert.Throws<EndOfStreamException>(() => reader.ReadNullTerminatedString());
    }

    [Fact]
    public void ReadBytes_ShorterThanTheFile_ReturnsExactlyTheRequest()
    {
        var payload = Payload(16);
        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", payload), bufferSize: 4);

        Assert.Equal(payload.Take(4), reader.ReadBytes(4));
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void ShortFile_ReadReturnsWhatTheFileActuallyHolds()
    {
        // The file claims 64 bytes but holds 10. `Read` reports the truth — 10 bytes — instead of throwing, which is the
        // whole point of the short-read contract: the caller learns the shortage from the return value. The declared
        // length is thereby not authoritative for how much data exists.
        var reader = new CustomFileReader(
            IoFiles.Truncated("short.bin", Payload(10), declaredLength: 64), bufferSize: 16);
        var buffer = new byte[64];

        Assert.Equal(10, reader.Read(buffer, 0, 64));
        Assert.Equal(Payload(10), buffer.Take(10));
        Assert.All(buffer.Skip(10), b => Assert.Equal(0, b));
    }

    [Fact]
    public void ShortFile_ReadBeyondTheRealEnd_ReturnsZero()
    {
        var reader = new CustomFileReader(
            IoFiles.Truncated("short.bin", Payload(10), declaredLength: 64), bufferSize: 16);

        Assert.Equal(10, reader.Read(new byte[16], 0, 16));
        Assert.Equal(0, reader.Read(new byte[16], 0, 16));
    }

    [Fact]
    public void ShortFile_ReadExactlyThrows()
    {
        var reader = new CustomFileReader(
            IoFiles.Truncated("short.bin", Payload(10), declaredLength: 64), bufferSize: 16);

        Assert.Throws<EndOfStreamException>(() => ((IReader)reader).ReadExactly(new byte[64]));
    }

    [Fact]
    public void ShortFile_ReadBytesThrowsOnceTheRealEndIsReached()
    {
        // ReadBytes guards on Remaining, which comes from the declared length, so it does not catch the shortage on its
        // own — the EndOfStream comes from ReadExactly underneath. Worth knowing: the declared length is trusted for
        // the bounds check.
        var reader = new CustomFileReader(
            IoFiles.Truncated("short.bin", Payload(10), declaredLength: 64), bufferSize: 16);

        Assert.Throws<EndOfStreamException>(() => reader.ReadBytes(32));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // MemoryReader: the same contract, no window.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void MemoryReader_Read_AcrossTheEndStopsAtTheEnd()
    {
        var reader = new MemoryReader(Payload(10));
        var buffer = new byte[16];

        Assert.Equal(10, reader.Read(buffer, 0, 16));
        Assert.Equal(Payload(10), buffer.Take(10));
        Assert.Equal(0, ((IReader)reader).Remaining);
    }

    [Fact]
    public void MemoryReader_ReadAtTheEnd_ReturnsZero()
    {
        var reader = new MemoryReader(Payload(4));
        reader.Position = 4;

        Assert.Equal(0, reader.Read(new byte[4], 0, 4));
    }

    [Fact]
    public void MemoryReader_ReadByteAtTheEnd_ThrowsEndOfStream()
    {
        // End of the source is reported as EndOfStreamException, the type IReader.ReadExactly already uses, so a caller
        // can catch "the reader ran out" without also catching an index error.
        var reader = new MemoryReader(Payload(4));
        reader.Position = 4;

        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }

    [Fact]
    public void MemoryReader_ReadBytePastTheEnd_ThrowsEndOfStream()
    {
        // A cursor at the end is the furthest legitimate state, and reading there is "nothing left" rather than an error.
        var reader = new MemoryReader(Payload(4));
        reader.Position = 4;

        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }

    [Fact]
    public void MemoryReader_PositionWithALargeLong_IsRefusedRatherThanTruncated()
    {
        // Position is a long on the interface and an int inside. A value the int cannot hold is refused by the range check
        // now, so it can no longer wrap into a negative cursor — which is what used to happen: `(int)value` truncated
        // silently and the reader reported a position that was never asked for.
        var reader = new MemoryReader(Payload(8));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = int.MaxValue + 1L);
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void MemoryReader_PositionAtTheEnd_ReadsNothing()
    {
        // The in-range half of the same question, pinned exactly: a cursor at the end is a short read of nothing.
        var reader = new MemoryReader(Payload(4));
        reader.Position = 4;

        Assert.Equal(4, reader.Position);
        Assert.Equal(0, ((IReader)reader).Remaining);
        Assert.Equal(0, reader.Read(new byte[4], 0, 4));
    }

    [Fact]
    public void MemoryReader_ProviderCreatesAnIndependentReaderPerCall()
    {
        var provider = new MemoryReaderProvider(Payload(8));

        var first = provider.CreateReader();
        var second = provider.CreateReader();
        first.ReadBytes(4);

        Assert.Equal(0, second.Position);
        Assert.Equal(8, ((IReader)second).Remaining);
    }

    [Fact]
    public void MemoryReader_AsReadOnlySpan_ExposesTheWholePayload()
    {
        var payload = Payload(8);
        var reader = new MemoryReader(payload);

        Assert.Equal(payload, reader.AsReadOnlySpan.ToArray());
    }

    private static byte[] Payload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)i;

        return payload;
    }
}