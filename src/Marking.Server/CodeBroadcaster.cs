using System.Collections.Concurrent;
using Marking.Protocol;
using Marking.Server.Gs1;
using Microsoft.Extensions.Logging;

namespace Marking.Server;

public sealed class CodeBroadcaster
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);
    private readonly ICodeGenerator _codeGenerator;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ClientSession, byte> _sessions = new ();
    public int ClientCount => _sessions.Count;

    public CodeBroadcaster(ICodeGenerator generator, ILogger<CodeBroadcaster> logger, TimeSpan? interval = null)
    {
        _interval =  interval ?? DefaultInterval;
        _codeGenerator = generator;
        _logger = logger;
    }
    
    public void Add(ClientSession session)
    {
        _sessions.TryAdd(session, 0);
    }

    public void Remove(ClientSession session)
    {
        _sessions.TryRemove(session, out _);
    }
    
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_interval);
            
            while (await timer.WaitForNextTickAsync(ct))
            {
                var code = _codeGenerator.Next();
                _logger.LogDebug("Код сгенерирован {Code}", GsSymbolReplacer.ReplaceGsSymbols(code));

                if (_sessions.IsEmpty)
                {
                    _logger.LogDebug("Код отброшен, клиентов нет");
                    continue;
                }

                foreach (var (session, _) in _sessions)
                {
                    if (!session.Enqueue(code))
                    {
                        Remove(session);
                        _logger.LogInformation("Сессия {Client} удалена", session.Name);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}