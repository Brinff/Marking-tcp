using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Marking.Client;
using Marking.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marking.Tests.Client;

public class MarkingClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class ListSink : ICodeSink
    {
        private readonly ConcurrentQueue<string> _items = new();
        public string[] Items => _items.ToArray();

        public Task WriteAsync(string code, CancellationToken ct)
        {
            _items.Enqueue(code);
            return Task.CompletedTask;
        }
    }
    
    private sealed class FakeServer : IDisposable
    {
        private readonly TcpListener _listener;
        public int Port { get; }

        public FakeServer(int port = 0)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public async Task<TcpClient> AcceptAsync()
            => await _listener.AcceptTcpClientAsync().WaitAsync(Timeout);

        public void Dispose() => _listener.Stop();
    }

    private static Task StartClient(int port, ListSink sink, CancellationToken ct)
    {
        var policy = new ReconnectPolicy(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50));
        var client = new MarkingClient("127.0.0.1", port, sink, policy, NullLogger<MarkingClient>.Instance);
        return client.RunAsync(ct);
    }

    private static async Task SendAsync(TcpClient server, params string[] messages)
    {
        foreach (var m in messages)
            await MessageFramer.WriteMessageAsync(server.GetStream(), m);
    }

    private static int FreePort()
    {
        using var probe = new FakeServer();
        return probe.Port;
    }

    [Fact]
    public async Task ReceivedCodes_AreWrittenToSinkInOrder()
    {
        using var server = new FakeServer();
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(server.Port, sink, cts.Token);

        using var conn = await server.AcceptAsync();
        await SendAsync(conn, "a", "b", "c");

        await Wait.UntilAsync(() => sink.Items.Length == 3);
        Assert.Equal(new[] { "a", "b", "c" }, sink.Items);

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ManyMessagesInSingleWrite_AreAllReceived()
    {
        using var server = new FakeServer();
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(server.Port, sink, cts.Token);
        using var conn = await server.AcceptAsync();
        
        var expected = Enumerable.Range(0, 200).Select(i => $"code{i}").ToArray();
        using var buffer = new MemoryStream();
        foreach (var m in expected)
            await MessageFramer.WriteMessageAsync(buffer, m);
        await conn.GetStream().WriteAsync(buffer.ToArray());

        await Wait.UntilAsync(() => sink.Items.Length == expected.Length);
        Assert.Equal(expected, sink.Items);

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ServerClosesConnection_ClientReconnectsAndContinues()
    {
        using var server = new FakeServer();
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(server.Port, sink, cts.Token);

        using (var first = await server.AcceptAsync())
        {
            await SendAsync(first, "before");
        } // сервер закрыл соединение
        using var second = await server.AcceptAsync();
        await SendAsync(second, "after");

        await Wait.UntilAsync(() => sink.Items.Length == 2);
        Assert.Equal(new[] { "before", "after" }, sink.Items);

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ServerNotRunningYet_ClientConnectsWhenItAppears()
    {
        int port = FreePort();
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(port, sink, cts.Token);

        await Task.Delay(150);
        using var server = new FakeServer(port);
        using var conn = await server.AcceptAsync();
        await SendAsync(conn, "late");

        await Wait.UntilAsync(() => sink.Items.Length == 1);

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task ProtocolViolation_ClientDropsConnectionAndReconnects()
    {
        using var server = new FakeServer();
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(server.Port, sink, cts.Token);

        using (var bad = await server.AcceptAsync())
            await bad.GetStream().WriteAsync(new byte[] { 0, 0, 0, 0 });
        using var good = await server.AcceptAsync();
        await SendAsync(good, "ok");

        await Wait.UntilAsync(() => sink.Items.Length == 1);
        Assert.Equal(new[] { "ok" }, sink.Items);

        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Cancellation_WhileWaitingForServer_CompletesWithoutException()
    {
        var sink = new ListSink();
        using var cts = new CancellationTokenSource();
        var run = StartClient(FreePort(), sink, cts.Token);

        await Task.Delay(100);
        await cts.CancelAsync();

        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Cancellation_WhileConnected_CompletesWithoutException()
    {
        using var server = new FakeServer();
        using var cts = new CancellationTokenSource();
        var run = StartClient(server.Port, new ListSink(), cts.Token);
        using var conn = await server.AcceptAsync();

        await cts.CancelAsync();

        await run.WaitAsync(Timeout);
    }
}