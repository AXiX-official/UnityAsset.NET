using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Tests.Support;
using Xunit;

namespace UnityAsset.NET.Tests;

/// <summary>
/// The write side of the Core IO layer: <see cref="CustomStreamWriter"/> plus the primitive helpers that live as
/// default implementations on <see cref="IWriter"/>.
/// <para>
/// The cases focus on what a buffered writer can get wrong quietly: a buffer boundary that shifts the output, a
/// <c>Position</c> that lands inside the pending buffer, endianness, alignment, and the two ways data can be lost —
/// a <c>Dispose</c> that does not flush, and a <c>Length</c> that does not count the buffer.
/// </para>
/// </summary>
public sealed class IWriterContractTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // Buffering: the bytes have to be identical whatever the buffer size, and Finish is what makes them visible.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(64)]
    [InlineData(8192)]
    public void WriteBytes_AnyBufferSize_ProducesTheSameBytes(int bufferSize)
    {
        var payload = Payload(300);
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: bufferSize);

        for (var offset = 0; offset < payload.Length; offset += 7)
        {
            var chunk = payload.AsSpan(offset, Math.Min(7, payload.Length - offset));
            writer.WriteBytes(chunk);
        }

        writer.Finish();

        Assert.Equal(payload, stream.ToArray());
    }

    [Fact]
    public void WriteBytes_LargerThanTheBuffer_GoesStraightToTheStream()
    {
        var payload = Payload(200);
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 16);

        writer.WriteBytes(payload);

        Assert.Equal(payload.Length, writer.Position);
        writer.Finish();
        Assert.Equal(payload, stream.ToArray());
    }

    [Fact]
    public void BufferedWrite_IsNotVisibleUntilFinish()
    {
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteBytes(Payload(10));

        Assert.Equal(10, writer.Position);     // logical position counts the buffer
        Assert.Equal(0, stream.Length);        // the stream itself does not yet

        writer.Finish();
        Assert.Equal(10, stream.Length);
    }

    [Fact]
    public void Length_ReportsTheStreamNotTheLogicalPosition()
    {
        // WriteBytes follows Position; Length does not. Anything that uses Length as "how much have I written" is off
        // by the pending buffer. Worth a test because it is the kind of difference that is invisible until it is not.
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteBytes(Payload(10));

        Assert.Equal(10, writer.Position);
        Assert.Equal(0, writer.Length);
    }

    [Fact]
    public void FlushAtEveryBufferBoundary_DoesNotShiftTheOutput()
    {
        // Write one byte past a 4-byte buffer repeatedly: every iteration exercises the flush-at-capacity path.
        for (var length = 1; length <= 33; length++)
        {
            var payload = Payload(length);
            using var stream = new MemoryStream();
            var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 4);

            writer.WriteBytes(payload);
            writer.Finish();

            Assert.Equal(payload, stream.ToArray());
            Assert.Equal(length, writer.Position);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Position: inside the pending buffer, forward, backward, and past the buffer.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Position_InsideThePendingBuffer_BackfillsWithZeros()
    {
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteByte(0xAA);
        writer.WriteByte(0xBB);

        writer.Position = 6;
        writer.WriteByte(0xCC);
        writer.Finish();

        Assert.Equal(new byte[] { 0xAA, 0xBB, 0, 0, 0, 0, 0xCC }, stream.ToArray());
    }

    [Fact]
    public void Position_BackwardsInsideTheBuffer_OverwritesWithoutDroppingTheRest()
    {
        // Moving the cursor back into the pending buffer must not lose what was already written there. The bytes after
        // the new position stay in the buffer and are flushed by Finish; only the byte that was actually overwritten
        // changes.
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteBytes(new byte[] { 1, 2, 3, 4, 5 });
        writer.Position = 1;
        writer.WriteByte(0xFF);
        writer.Finish();

        Assert.Equal(new byte[] { 1, 0xFF, 3, 4, 5 }, stream.ToArray());
    }

    [Fact]
    public void Position_ForwardInsideTheBuffer_DoesNotRefillOrDropWhatIsBuffered()
    {
        // The counterpart of the rewind: moving forward inside the pending window pads the gap and keeps the cursor
        // consistent. It has to leave the bytes after the write alone in the same way.
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteBytes(new byte[] { 1, 2, 3, 4, 5 });
        writer.Position = 3;
        writer.WriteByte(0xFF);
        writer.Finish();

        // Moving to 3 is a rewind here, so the stream has the five bytes; the write overwrites index 3.
        Assert.Equal(new byte[] { 1, 2, 3, 0xFF, 5 }, stream.ToArray());
    }

    [Fact]
    public void Position_BeyondTheBuffer_FlushesAndLandsExactly()
    {
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 8);

        writer.WriteBytes(Payload(4));
        writer.Position = 40;          // outside the window: the buffer is flushed and the stream seeked
        writer.WriteByte(0x7F);
        writer.Finish();

        var bytes = stream.ToArray();
        Assert.Equal(41, bytes.Length);
        Assert.Equal(0x7F, bytes[40]);
        Assert.Equal(0, bytes[39]);
        Assert.Equal(41, writer.Position);
    }

    [Fact]
    public void Position_SetToTheSameValue_IsANoOp()
    {
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        writer.WriteBytes(new byte[] { 1, 2, 3 });
        writer.Position = writer.Position;
        writer.WriteByte(4);
        writer.Finish();

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, stream.ToArray());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Endianness and primitives.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WriteUInt32_HonoursEndianness()
    {
        Assert.Equal(
            new byte[] { 0x12, 0x34, 0x56, 0x78 },
            Write(Endianness.BigEndian, w => w.WriteUInt32(0x12345678)));

        Assert.Equal(
            new byte[] { 0x78, 0x56, 0x34, 0x12 },
            Write(Endianness.LittleEndian, w => w.WriteUInt32(0x12345678)));
    }

    [Fact]
    public void WriteInt16AndUInt16_HonourEndianness()
    {
        Assert.Equal(new byte[] { 0x12, 0x34 }, Write(Endianness.BigEndian, w => w.WriteInt16(0x1234)));
        Assert.Equal(new byte[] { 0x34, 0x12 }, Write(Endianness.LittleEndian, w => w.WriteInt16(0x1234)));
        Assert.Equal(new byte[] { 0xFF, 0xFE }, Write(Endianness.BigEndian, w => w.WriteUInt16(0xFFFE)));
        Assert.Equal(new byte[] { 0xFE, 0xFF }, Write(Endianness.LittleEndian, w => w.WriteUInt16(0xFFFE)));
    }

    [Fact]
    public void WriteInt64AndUInt64_HonourEndianness()
    {
        Assert.Equal(
            new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
            Write(Endianness.BigEndian, w => w.WriteInt64(0x0102030405060708)));

        Assert.Equal(
            new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 },
            Write(Endianness.LittleEndian, w => w.WriteUInt64(0x0102030405060708)));
    }

    [Fact]
    public void WriteSingle_HonoursEndianness()
    {
        Assert.Equal(new byte[] { 0x3F, 0x80, 0x00, 0x00 }, Write(Endianness.BigEndian, w => w.WriteSingle(1.0f)));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, Write(Endianness.LittleEndian, w => w.WriteSingle(1.0f)));
    }

    [Fact]
    public void WriteByteAndBoolean_AreSingleBytes()
    {
        Assert.Equal(new byte[] { 0xAB }, Write(Endianness.BigEndian, w => w.WriteByte(0xAB)));
        Assert.Equal(new byte[] { 0x01 }, Write(Endianness.BigEndian, w => w.WriteBoolean(true)));
        Assert.Equal(new byte[] { 0x00 }, Write(Endianness.BigEndian, w => w.WriteBoolean(false)));
    }

    [Fact]
    public void WriteNullTerminatedString_IsUtf8PlusATerminator()
    {
        var bytes = Write(Endianness.BigEndian, w => w.WriteNullTerminatedString("ab"));

        Assert.Equal(new byte[] { (byte)'a', (byte)'b', 0x00 }, bytes);
    }

    [Fact]
    public void WriteSizedString_PrefixesTheByteCountAndPadsToFourBytes()
    {
        // The contract a reader needs: the prefix is the number of bytes that follow, and the whole field ends on a
        // 4-byte boundary. IReader.ReadSizedString reads `length` bytes and then Aligns to 4, so both halves have to hold
        // for a round trip to land on the same position.
        var ascii = Write(Endianness.LittleEndian, w => w.WriteSizedString("abc"));
        Assert.Equal(
            new byte[] { 0x03, 0x00, 0x00, 0x00, (byte)'a', (byte)'b', (byte)'c', 0x00 },
            ascii);

        // Three UTF-8 bytes: 4 + 3 = 7, padded to 8 on the 4-byte boundary.
        var wide = Write(Endianness.LittleEndian, w => w.WriteSizedString("你"));
        Assert.Equal(8, wide.Length);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x00, 0x00 }, wide.Take(4));
    }

    [Fact]
    public void WriteSizedString_AndReadSizedString_LandOnTheSamePosition()
    {
        // The round trip the two members have to agree on. Reading it back through an IReader is the only way to catch a
        // prefix that counts characters while the reader counts bytes.
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, endian: Endianness.LittleEndian, leaveOpen: true);
        ((IWriter)writer).WriteSizedString("abc");
        ((IWriter)writer).WriteSizedString("你");
        ((IWriter)writer).WriteInt32(0x1234);
        writer.Finish();

        var reader = (IReader)new MemoryReader(stream.ToArray(), endian: Endianness.LittleEndian);
        Assert.Equal("abc", reader.ReadSizedString());
        Assert.Equal("你", reader.ReadSizedString());
        Assert.Equal(0x1234, reader.ReadInt32());
    }

    [Fact]
    public void WriteBytesPattern_WritesTheRequestedRun()
    {
        var bytes = Write(Endianness.BigEndian, w => w.WriteBytes(0x2A, 5));

        Assert.Equal(new byte[] { 0x2A, 0x2A, 0x2A, 0x2A, 0x2A }, bytes);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Align.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    public void Align_PadsToTheNextMultipleOfFour(int payloadLength, int expectedLength)
    {
        var payload = Payload(payloadLength);
        var bytes = Write(Endianness.BigEndian, w =>
        {
            w.WriteBytes(payload);
            w.Align(4);
        });

        Assert.Equal(expectedLength, bytes.Length);
        Assert.Equal(payload, bytes.Take(payloadLength));
        Assert.All(bytes.Skip(payloadLength), b => Assert.Equal(0, b));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Copying from a reader.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WriteBytesFromReader_CopiesEverythingAndReportsTheCount()
    {
        var payload = Payload(1000);
        var reader = new MemoryReader(payload);
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64);

        var copied = writer.WriteBytes(reader);

        writer.Finish();
        Assert.Equal((ulong)payload.Length, copied);
        Assert.Equal(payload, stream.ToArray());
        Assert.Equal(0, ((IReader)reader).Remaining);
    }

    [Fact]
    public void WriteBytesFromReader_FromAnAlreadyAdvancedCursor_CopiesTheRemainderOnly()
    {
        var payload = Payload(100);
        var reader = new MemoryReader(payload);
        reader.ReadBytes(60);

        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 16);
        var copied = writer.WriteBytes(reader);
        writer.Finish();

        Assert.Equal(40ul, copied);
        Assert.Equal(payload.Skip(60), stream.ToArray());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The two ways to lose data, pinned so a change to either is visible.
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Dispose_WithoutFinish_StillWritesThePendingBuffer()
    {
        // A `using` around the writer never calls Finish, so disposing has to hand over the buffered tail. Otherwise a
        // write smaller than one buffer — the normal case for a small file — reaches nothing at all.
        var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 64))
        {
            writer.WriteBytes(Payload(10));
        }

        Assert.Equal(10, stream.Length);
    }

    [Fact]
    public void Dispose_WithoutFinish_KeepsTheBytesThatWereAlreadyFlushed()
    {
        // Seen from the buffered side: three bytes through a 4-byte buffer stay pending until disposal, so those three
        // are exactly what has to arrive.
        var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 4))
        {
            writer.WriteBytes(Payload(3));
        }

        Assert.Equal(3, stream.Length);
        Assert.Equal(Payload(3), stream.ToArray());
    }

    [Fact]
    public void Dispose_AfterFinish_DoesNotWriteAnythingTwice()
    {
        // Flushing in Dispose must not duplicate what Finish already wrote.
        var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 4))
        {
            writer.WriteBytes(Payload(11));
            writer.Finish();
        }

        Assert.Equal(11, stream.Length);
        Assert.Equal(Payload(11), stream.ToArray());
    }

    [Fact]
    public void WriteBytes_LargerThanTheBuffer_FlushesEverythingByItself()
    {
        // The other side of the same coin: a write that fills or overflows the buffer flushes as it goes, so those
        // bytes survive even without Finish. Only the tail that stays in the buffer is at risk.
        var stream = new MemoryStream();
        using (var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 4))
        {
            writer.WriteBytes(Payload(11));
        }

        Assert.Equal(11, stream.Length);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The writer over a real file: buffered writes have to reach the disk, and seeking has to land exactly.
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(8)]      // smaller than one write: every write goes straight through
    [InlineData(64)]     // spans the buffer boundary
    [InlineData(8192)]   // a single buffer holds it all
    public void FileRoundTrip_AnyBufferSize_ReadsBackTheSameBytes(int bufferSize)
    {
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "payload.bin");
        var payload = Payload(300);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: bufferSize);
            writer.WriteBytes(payload);
            writer.Finish();
        }

        var readBack = new CustomFileReader(IoFiles.Raw("payload.bin", File.ReadAllBytes(path)))
            .ReadBytes(300);

        Assert.Equal(payload, readBack);
    }

    [Fact]
    public void FileWriter_WriteThenReadBackFromTheSameFile()
    {
        // The writer holds the file open for the duration, and the reader here is built from a byte array because a
        // second handle on the same path would fight with the writer's. What this pins is that Finish puts the whole
        // payload on disk in the right order.
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Path, "payload.bin");

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var writer = new CustomStreamWriter(stream, leaveOpen: true, bufferSize: 16);
            var target = (IWriter)writer;
            target.WriteUInt32(0xDEADBEEF);
            target.WriteNullTerminatedString("name");
            writer.Finish();
            Assert.Equal(9, stream.Length);
        }

        var reader = new CustomFileReader(IoFiles.Raw("payload.bin", File.ReadAllBytes(path)))
            { Endian = Endianness.BigEndian };

        Assert.Equal(0xDEADBEEFu, reader.ReadUInt32());
        Assert.Equal("name", reader.ReadNullTerminatedString());
    }

    private static byte[] Write(Endianness endian, Action<IWriter> write)
    {
        using var stream = new MemoryStream();
        var writer = new CustomStreamWriter(stream, endian: endian, leaveOpen: true);
        var target = (IWriter)writer;
        write(target);
        writer.Finish();
        return stream.ToArray();
    }

    private static byte[] Payload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i % 251);

        return payload;
    }
}