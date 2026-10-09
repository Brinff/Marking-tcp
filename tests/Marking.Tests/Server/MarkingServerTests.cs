using System.Net;
using System.Net.Sockets;
using Marking.Protocol;
using Marking.Server;
using Marking.Server.Gs1;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marking.Tests.Server;

public class MarkingServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class CounterGenerator : ICodeGenerator
    {
        private int _n;
        public string Next() => $"code{Interlocked.Increment(ref _n)}";
    }
    
    private sealed class Harness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _broadcasterRun;
        public Task ServerRun { get; }
        public CodeBroadcaster Broadcaster { get; }
        public MarkingServer Server { get; }

        public Harness()
        {
            Broadcaster = new CodeBroadcaster(
                new CounterGenerator(), NullLogger<CodeBroadcaster>.Instance, TimeSpan.FromMilliseconds(10));
            Server = new MarkingServer(IPAddress.Loopback, 0, Broadcaster, NullLoggerFactory.Instance);
            Server.Start();
            _broadcasterRun = Broadcaster.RunAsync(_cts.Token);
            ServerRun = Server.RunAsync(_cts.Token);
        }

        public async Task<TcpClient> ConnectAsync()
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, Server.Port);
            return client;
        }

        public Task StopAsync() => _cts.CancelAsync();

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            await Task.WhenAll(_broadcasterRun, ServerRun).WaitAsync(Timeout);
            _cts.Dispose();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(condition(), "Условие не выполнилось за отведённое время");
    }

    [Fact]
    public async Task Start_WithPortZero_AssignsRealPort()
    {
        await using var h = new Harness();

        Assert.InRange(h.Server.Port, 1, 65535);
    }

    [Fact]
    public async Task ConnectedClient_ReceivesCodes()
    {
        await using var h = new Harness();
        using var client = await h.ConnectAsync();

        var message = await MessageFramer.ReadMessageAsync(client.GetStream()).WaitAsync(Timeout);

        Assert.StartsWith("code", message);
    }

    [Fact]
    public async Task TwoClients_BothReceiveCodes()
    {
        await using var h = new Harness();
        using var a = await h.ConnectAsync();
        using var b = await h.ConnectAsync();

        Assert.NotNull(await MessageFramer.ReadMessageAsync(a.GetStream()).WaitAsync(Timeout));
        Assert.NotNull(await MessageFramer.ReadMessageAsync(b.GetStream()).WaitAsync(Timeout));
        Assert.Equal(2, h.Broadcaster.ClientCount);
    }

    [Fact]
    public async Task ClientDisconnects_IsRemovedFromRegistry()
    {
        await using var h = new Harness();
        var client = await h.ConnectAsync();
        await MessageFramer.ReadMessageAsync(client.GetStream()).WaitAsync(Timeout);

        client.Dispose();

        await WaitUntilAsync(() => h.Broadcaster.ClientCount == 0);
    }

    [Fact]
    public async Task ClientReconnects_ReceivesCodesAgain()
    {
        await using var h = new Harness();
        var first = await h.ConnectAsync();
        await MessageFramer.ReadMessageAsync(first.GetStream()).WaitAsync(Timeout);
        first.Dispose();
        await WaitUntilAsync(() => h.Broadcaster.ClientCount == 0);

        using var second = await h.ConnectAsync();
        var message = await MessageFramer.ReadMessageAsync(second.GetStream()).WaitAsync(Timeout);

        Assert.StartsWith("code", message);
    }

    [Fact]
    public async Task Cancellation_StopsServerAndClosesClientConnection()
    {
        await using var h = new Harness();
        using var client = await h.ConnectAsync();
        var stream = client.GetStream();
        await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout);

        await h.StopAsync();

        await h.ServerRun.WaitAsync(Timeout);
        string? last;
        do { last = await MessageFramer.ReadMessageAsync(stream).WaitAsync(Timeout); }
        while (last is not null);
    }
}