using Marking.Protocol;
using Marking.Server;
using Marking.Server.Gs1;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marking.Tests.Server;

public class CodeBroadcasterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);

    private sealed class CounterGenerator : ICodeGenerator
    {
        private int _n;
        public string Next() => $"code{Interlocked.Increment(ref _n)}";
    }

    private static CodeBroadcaster Create()
        => new(new CounterGenerator(), NullLogger<CodeBroadcaster>.Instance, Tick);

    [Fact]
    public async Task Code_IsDeliveredToAllClients()
    {
        var (s1, c1) = await TcpPair.CreateAsync();
        var (s2, c2) = await TcpPair.CreateAsync();
        using var _1 = s1; using var _2 = c1; using var _3 = s2; using var _4 = c2;
        using var cts = new CancellationTokenSource();

        var a = new ClientSession(s1.GetStream(), "a", NullLogger.Instance);
        var b = new ClientSession(s2.GetStream(), "b", NullLogger.Instance);
        var runA = a.RunAsync(cts.Token);
        var runB = b.RunAsync(cts.Token);

        var broadcaster = Create();
        broadcaster.Add(a);
        broadcaster.Add(b);
        var run = broadcaster.RunAsync(cts.Token);

        var m1 = await MessageFramer.ReadMessageAsync(c1.GetStream()).WaitAsync(Timeout);
        var m2 = await MessageFramer.ReadMessageAsync(c2.GetStream()).WaitAsync(Timeout);
        Assert.StartsWith("code", m1);
        Assert.StartsWith("code", m2);

        await cts.CancelAsync();
        await Task.WhenAll(run, runA, runB).WaitAsync(Timeout);
    }

    [Fact]
    public async Task NoClients_RunsAndStopsWithoutException()
    {
        using var cts = new CancellationTokenSource();
        var run = Create().RunAsync(cts.Token);

        await Task.Delay(100);
        await cts.CancelAsync();

        await run.WaitAsync(Timeout);
    }

    [Fact]
    public async Task DeadSession_IsRemovedFromRegistry()
    {
        var (server, client) = await TcpPair.CreateAsync();
        using var _ = server;
        var session = new ClientSession(server.GetStream(), "dead", NullLogger.Instance);
        var sessionRun = session.RunAsync(CancellationToken.None);
        client.Dispose();
        await sessionRun.WaitAsync(Timeout);

        using var cts = new CancellationTokenSource();
        var broadcaster = Create();
        broadcaster.Add(session);
        var run = broadcaster.RunAsync(cts.Token);

        var deadline = DateTime.UtcNow + Timeout;
        while (broadcaster.ClientCount > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal(0, broadcaster.ClientCount);
        await cts.CancelAsync();
        await run.WaitAsync(Timeout);
    }
}