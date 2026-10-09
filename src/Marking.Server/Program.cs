using Marking.Server;
using Marking.Server.Gs1;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

string ip = "127.0.0.1";
int port = 5000;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--ip" when i + 1 < args.Length:
            ip = args[++i];
            break;

        case "--port" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out port) || port is < 1 or > 65535)
            {
                Console.Error.WriteLine("Порт должен быть от 1 до 65535.");
                return 1; 
            }
            break;

        case "--ip":
        case "--port":
            Console.Error.WriteLine($"Отсутствует значение для: {args[i]}.");
            return 1;

        default:
            Console.Error.WriteLine($"Неизвестный аргумент: {args[i]}");
            return 1;
    }
}

if (!IPAddress.TryParse(ip, out var address))
{
    Console.Error.WriteLine($"Невалидный IP адрес: {ip}");
    return 1;
}

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Debug));
var programmLogger = loggerFactory.CreateLogger<Program>();

Gs1CodeGenerator codeGenerator = new Gs1CodeGenerator(new Random());
CodeBroadcaster codeBroadcaster = new CodeBroadcaster(codeGenerator,  loggerFactory.CreateLogger<CodeBroadcaster>());
MarkingServer markingServer = new MarkingServer(address, port, codeBroadcaster, loggerFactory);

try
{
    markingServer.Start();
}
catch (SocketException e)
{
    programmLogger.LogCritical(e, "Не удалось занять порт");
    return 1;
}
catch (Exception e)
{
    programmLogger.LogCritical(e, "Ошибка старта сервера на заданном порте");
    return 1;
}

var codeBroadcasterTask = codeBroadcaster.RunAsync(cts.Token);
var markingServerTask = markingServer.RunAsync(cts.Token);

await Task.WhenAny(codeBroadcasterTask, markingServerTask);
await cts.CancelAsync();
await Task.WhenAll(codeBroadcasterTask, markingServerTask);

return 0;