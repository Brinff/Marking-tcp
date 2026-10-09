using Marking.Client;
using Microsoft.Extensions.Logging;

string host = "127.0.0.1";
int port = 5000;
string output = "codes.txt";
TimeSpan initial = TimeSpan.FromSeconds(1);
TimeSpan max = TimeSpan.FromSeconds(10);

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--host" when i + 1 < args.Length:
            host = args[++i];
            break;

        case "--port" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out port) || port is < 1 or > 65535)
            {
                Console.Error.WriteLine("Порт должен быть от 1 до 65535.");
                return 1; 
            }
            break;
        
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;

        case "--host":
        case "--port":
        case "--output":
            Console.Error.WriteLine($"Отсутствует значение для: {args[i]}.");
            return 1;

        default:
            Console.Error.WriteLine($"Неизвестный аргумент: {args[i]}");
            return 1;
    }
}

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Debug));

    await using var codeFileWriter = new CodeFileWriter(output);
    var reconnectPolicy = new ReconnectPolicy(initial, max);
    var markingClient = new MarkingClient(host, port, codeFileWriter, reconnectPolicy,
        loggerFactory.CreateLogger<MarkingClient>());

    await markingClient.RunAsync(cts.Token);
    return 0;
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    return 0;
}
catch (CodeSinkException e)
{
    Console.Error.WriteLine($"Ошибка записи в файл: {e.InnerException?.Message ?? e.Message}");
    return 1;
}
catch (Exception e)
{
    Console.Error.WriteLine($"Ошибка клиента: {e.Message}");
    return 1;
}