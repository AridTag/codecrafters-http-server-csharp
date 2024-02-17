using System.Buffers;
using System.CommandLine;
using System.CommandLine.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HttpServer;

internal static class Program
{
    private static TcpListener _Listener = null!;
    private static RequestHandler _RequestHandler = null!;
    
    public static async Task Main(string[] args)
    {
        var directoryOption = new Option<DirectoryInfo?>(
            "--directory",
            "files root"
        );
        
        var root = new RootCommand
        {
            directoryOption
        };
        
        root.SetHandler(async directory =>
        {
            var cancellation = new CancellationTokenSource();
            await RunServer(directory, cancellation.Token);
        }, directoryOption);

        await root.InvokeAsync(args, new SystemConsole());
    }

    private static async Task RunServer(DirectoryInfo? filesRoot, CancellationToken cancellationToken)
    {
        using var childTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationToken = childTokenSource.Token;
        
        var log = FauxLogger.Instance;

        await TaskObserver.Instance.Start(cancellationToken);

        _RequestHandler = new RequestHandler(filesRoot);
        _Listener = new TcpListener(IPAddress.Any, 4221);
        _Listener.Start();
        log.LogInformation("Now accepting connections");

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await AcceptClient();
            TaskObserver.Instance.Register(_RequestHandler.Handle(client).ContinueWith(t => client.Dispose(), CancellationToken.None));
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