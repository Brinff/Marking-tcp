using Marking.Protocol;
using Marking.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marking.Tests.Server;

public class ClientSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static ClientSession Create(Stream stream, int capacity = 100)
        => new(stream, "test", NullLogger.Instance, capacity);

    [Fact]
    public async Task EnqueuedCodes_ArriveToClientInOrder()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server; using var __ = client;
        using var cts = new CancellationTokenSource();
        var session = Create(server.GetStream());
        var run = session.RunAsync(cts.Token);

        session.Enqueue("one");
        session.Enqueue("two");
        session.Enqueue("three");

        var stream = client.GetStream();
        Assert.Equal("one", await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout));
        Assert.Equal("two", await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout));
        Assert.Equal("three", await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout));

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ClientDisconnects_RunCompletesWithoutException()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server;
        var session = Create(server.GetStream());
        var run = session.RunAsync(CancellationToken.None);

        client.Dispose();

        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Cancellation_StopsSessionWithoutException()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server; using var __ = client;
        using var cts = new CancellationTokenSource();
        var run = Create(server.GetStream()).RunAsync(cts.Token);

        await cts.CancelAsync();

        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task AfterRunCompleted_EnqueueReturnsFalse()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server;
        var session = Create(server.GetStream());
        var run = session.RunAsync(CancellationToken.None);
        client.Dispose();
        await run.WaitAsync(Timeout);

        Assert.False(session.Enqueue("late"));
    }

    [Fact]
    public async Task QueueOverflow_DropsOldestCodes()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server; using var __ = client;
        using var cts = new CancellationTokenSource();
        var session = Create(server.GetStream(), capacity: 2);
        
        for (int i = 1; i <= 5; i++)
            Assert.True(session.Enqueue($"code{i}"));
        var run = session.RunAsync(cts.Token);

        var stream = client.GetStream();
        Assert.Equal("code4", await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout));
        Assert.Equal("code5", await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout));

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }
}