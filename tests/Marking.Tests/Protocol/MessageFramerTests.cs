using System.Buffers.Binary;
using System.Text;
using Marking.Protocol;

namespace Marking.Tests.Protocol;

public class MessageFramerTests
{
    private const string GsCode = "0104006381333931215Zx9aKq2mB\u001D91EE10";

    private static async Task<byte[]> FrameAsync(params string[] messages)
    {
        using var ms = new MemoryStream();
        foreach (var m in messages)
            await MessageFramer.WriteMessageAsync(ms, m);
        return ms.ToArray();
    }

    private static byte[] RawFrame(int declaredLength, byte[]? payload = null)
    {
        var buf = new byte[ProtocolConstants.HeaderSize + (payload?.Length ?? 0)];
        BinaryPrimitives.WriteInt32BigEndian(buf, declaredLength);
        payload?.CopyTo(buf, ProtocolConstants.HeaderSize);
        return buf;
    }

    [Fact]
    public async Task NaiveRead_MergesTwoMessagesIntoOne()
    {
        var wire = Encoding.UTF8.GetBytes("AAA" + "BBB");
        using var stream = new MemoryStream(wire);
        var buffer = new byte[16];

        int n = await stream.ReadAsync(buffer);

        Assert.Equal(6, n);
    }

    [Fact]
    public async Task Write_ProducesBigEndianLengthHeaderThenUtf8Payload()
    {
        var bytes = await FrameAsync("AB");

        Assert.Equal(new byte[] { 0, 0, 0, 2, 0x41, 0x42 }, bytes);
    }

    [Theory]
    [InlineData("")]
    public async Task Write_EmptyMessage_Throws(string message)
    {
        using var ms = new MemoryStream();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => MessageFramer.WriteMessageAsync(ms, message));
    }

    [Fact]
    public async Task Write_TooLongMessage_Throws()
    {
        using var ms = new MemoryStream();
        var tooLong = new string('x', ProtocolConstants.MaxMessageLength + 1);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => MessageFramer.WriteMessageAsync(ms, tooLong));
        Assert.Equal(0, ms.Length);
    }

    [Theory]
    [InlineData("A")]
    [InlineData(GsCode)]
    [InlineData("Привет, 世界")]
    public async Task RoundTrip_ReturnsSameMessage(string message)
    {
        using var stream = new MemoryStream(await FrameAsync(message));

        var result = await MessageFramer.ReadMessageAsync(stream);

        Assert.Equal(message, result);
    }

    [Fact]
    public async Task Read_StuckTogetherMessages_AreSplitCorrectly()
    {
        using var stream = new MemoryStream(await FrameAsync("one", "two", "three"));

        Assert.Equal("one", await MessageFramer.ReadMessageAsync(stream));
        Assert.Equal("two", await MessageFramer.ReadMessageAsync(stream));
        Assert.Equal("three", await MessageFramer.ReadMessageAsync(stream));
        Assert.Null(await MessageFramer.ReadMessageAsync(stream));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task Read_SplitDelivery_ReassemblesMessages(int chunkSize)
    {
        using var stream = new ChunkedStream(await FrameAsync(GsCode, "second"), chunkSize);

        Assert.Equal(GsCode, await MessageFramer.ReadMessageAsync(stream));
        Assert.Equal("second", await MessageFramer.ReadMessageAsync(stream));
        Assert.Null(await MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        Assert.Null(await MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_StreamEndsInsideHeader_ThrowsProtocolException()
    {
        using var stream = new MemoryStream(new byte[] { 0, 0 });

        await Assert.ThrowsAsync<ProtocolException>(
            () => MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_StreamEndsInsidePayload_ThrowsProtocolException()
    {
        var full = await FrameAsync("hello");
        using var stream = new MemoryStream(full[..^2]);

        await Assert.ThrowsAsync<ProtocolException>(
            () => MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_HeaderOnlyThenEof_ThrowsProtocolException()
    {
        using var stream = new MemoryStream(RawFrame(5));

        await Assert.ThrowsAsync<ProtocolException>(
            () => MessageFramer.ReadMessageAsync(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ProtocolConstants.MaxMessageLength + 1)]
    [InlineData(int.MaxValue)]
    public async Task Read_InvalidLength_ThrowsProtocolException(int declaredLength)
    {
        using var stream = new MemoryStream(RawFrame(declaredLength));

        await Assert.ThrowsAsync<ProtocolException>(
            () => MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_InvalidUtf8_ThrowsProtocolException()
    {
        using var stream = new MemoryStream(RawFrame(2, new byte[] { 0xFF, 0xFE }));

        await Assert.ThrowsAsync<ProtocolException>(
            () => MessageFramer.ReadMessageAsync(stream));
    }

    [Fact]
    public async Task Read_CancelledToken_ThrowsOperationCanceled()
    {
        using var stream = new MemoryStream(await FrameAsync("x"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MessageFramer.ReadMessageAsync(stream, cts.Token));
    }
}