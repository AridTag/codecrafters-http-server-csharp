using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HttpServer;

internal static class Program
{
    private static TcpListener _Listener = null!;
    
    public static async Task Main(string[] args)
    {
        var cancellation = new CancellationTokenSource();
        await RunServer(cancellation.Token);
    }

    private static async Task RunServer(CancellationToken cancellationToken)
    {
        using var childTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = childTokenSource.Token;
        
        var log = FauxLogger.Instance;

        await TaskObserver.Instance.Start(cancellationToken);
        
        _Listener = new TcpListener(IPAddress.Any, 4221);
        _Listener.Start();
        log.LogInformation("Now accepting connections");

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await AcceptClient();
            TaskObserver.Instance.Register(client.HandleRequest().ContinueWith(t => client.Dispose(), CancellationToken.None));
        }

        await TaskObserver.Instance.StopWorker();

        if (!childTokenSource.IsCancellationRequested)
        {
            childTokenSource.Cancel();
        }
        
        _Listener.Stop();
    }

    private static async Task<ClientSession> AcceptClient()
    {
        var client = await _Listener.AcceptTcpClientAsync();
        return new ClientSession(client);
    }
}