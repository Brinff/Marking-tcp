using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Marking.Server;

public sealed class MarkingServer
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly CodeBroadcaster _codeBroadcaster;
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<Task, byte> _clientTasks = new();
    
    public int Port { get; private set; }
    
    public MarkingServer(IPAddress address, int port, CodeBroadcaster broadcaster, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MarkingServer>();
        _codeBroadcaster = broadcaster;
        _listener = new TcpListener(address, port);
    }
    
    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        ClientSession? clientSession = null;
        
        try
        {
            var name = tcp.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            tcp.NoDelay = true;
            tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            clientSession = new(tcp.GetStream(), name, _loggerFactory.CreateLogger<ClientSession>());

            _codeBroadcaster.Add(clientSession);
            await clientSession.RunAsync(ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Ошибка настройки и запуска клиента");
        }
        finally
        {
            if (clientSession != null) _codeBroadcaster.Remove(clientSession);
            tcp.Dispose();
        }
    }
    
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tcp = await _listener.AcceptTcpClientAsync(ct);
                    var task = HandleClientAsync(tcp, ct);
                    _clientTasks.TryAdd(task, 0);
                    _ = task.ContinueWith(t => _clientTasks.TryRemove(t, out _), TaskScheduler.Default);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    _logger.LogError(e, "Ошибка внутри AcceptTcpClientAsync");
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
            await Task.WhenAll(_clientTasks.Keys);
        }
    }
}