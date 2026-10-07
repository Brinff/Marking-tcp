using System.Threading.Channels;
using Marking.Protocol;
using Microsoft.Extensions.Logging;

namespace Marking.Server;

public sealed class ClientSession
{
    private readonly Stream _stream;
    private readonly ILogger _logger;
    private readonly Channel<string> _queue;
    public string Name { get; }
    
    public ClientSession(Stream stream, string name, ILogger logger, int queueCapacity = 100)
    {
        _stream = stream;
        Name = name;
        _logger = logger;
        var channelOptions = new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader =  true
        };
        _queue = Channel.CreateBounded<string>(channelOptions);
    }

    public bool Enqueue(string code)
    {
        return _queue.Writer.TryWrite(code);
    }
    
    public async Task RunAsync(CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        var send = SendLoopAsync(linkedCts.Token);
        var watch = WatchDisconnectAsync(linkedCts.Token);

        try
        {
            await Task.WhenAny(send, watch);
            await linkedCts.CancelAsync();
            await Task.WhenAll(send, watch);
        }
        finally
        {
            _queue.Writer.TryComplete();
        }
    }
    
    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var code in _queue.Reader.ReadAllAsync(ct))
            {
                await MessageFramer.WriteMessageAsync(_stream, code, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Связь с клиентом {Client} потеряна: {Reason}", Name, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Ошибка в SendLoopAsync");
        }
    }
    
    private async Task WatchDisconnectAsync(CancellationToken ct)
    {
        var buffer = new byte[1];
        try
        {
            while (true)
            {
                int n = await _stream.ReadAsync(buffer, ct);
                if (n == 0)
                { 
                    _logger.LogInformation("Клиент {Client} закрыл соединение", Name);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Связь с клиентом {Client} потеряна: {Reason}", Name, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Ошибка в WatchDisconnectAsync");
        }
    }
}