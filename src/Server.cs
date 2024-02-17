using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpServer;

internal static class Program
{
    private static TcpListener _Listener = null!;
    
    public static async Task Main(string[] args)
    {
        _Listener = new TcpListener(IPAddress.Any, 4221);
        _Listener.Start();

        while (true)
        {
            using var client = await AcceptClient();
            await client.HandleRequest();
        }
        
        _Listener.Stop();
    }

    private static async Task<ClientSession> AcceptClient()
    {
        var client = await _Listener.AcceptTcpClientAsync();
        return new ClientSession(client);
    }
}
