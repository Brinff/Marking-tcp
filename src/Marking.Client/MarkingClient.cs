using System.Net.Sockets;
using Marking.Protocol;
using Microsoft.Extensions.Logging;

namespace Marking.Client;

public sealed class MarkingClient
{
    private readonly ILogger _logger;
    private readonly ReconnectPolicy _reconnectPolicy;
    private readonly ICodeSink _codeSink;
    private readonly string _hostname;
    private readonly int _port;
    
    public MarkingClient(string host, int port, ICodeSink sink, ReconnectPolicy policy, ILogger<MarkingClient> logger)
    {
        _hostname = host;
        _port = port;
        _codeSink = sink;
        _reconnectPolicy = policy;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var tcp = new TcpClient();
                tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
                tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                
                await tcp.ConnectAsync(_hostname, _port, ct);
                
                _logger.LogInformation("Подключено к {Host}:{Port}", _hostname, _port);
                
                var stream = tcp.GetStream();
                bool receivedMessage = false;
                
                while (await MessageFramer.ReadMessageAsync(stream, ct) is { } code)
                {
                    try { await _codeSink.WriteAsync(code, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        throw new CodeSinkException("Не удалось записать код", ex);
                    }

                    if (!receivedMessage)
                    {
                        receivedMessage = true;
                        _reconnectPolicy.Reset();
                    }

                    _logger.LogDebug("Получен код: {Code}", GsSymbolReplacer.ReplaceGsSymbols(code));
                }
                
                _logger.LogWarning("Сервер закрыл соединение");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e) when (e is SocketException or IOException or ProtocolException)
            {
                _logger.LogWarning("Соединение потеряно: {Reason}", e.Message);
            }
            catch (Exception e) when (e is not CodeSinkException)
            {
                _logger.LogError(e, "Неожиданная ошибка");
            }
            
            if (ct.IsCancellationRequested)
                break;

            var delay = _reconnectPolicy.NextDelay();

            _logger.LogInformation("Повторное подключение через {Delay:F1} секунд", delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
        
        _logger.LogInformation("Клиент завершил работу");
    }
}