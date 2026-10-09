using System.Net;
using System.Net.Sockets;

namespace Marking.Tests.Server;

internal static class TcpPair
{
    public static async Task<(TcpClient Server, TcpClient Client)> CreateAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(IPAddress.Loopback, port);
            return (await acceptTask, client);
        }
        finally
        {
            listener.Stop();
        }
    }
}