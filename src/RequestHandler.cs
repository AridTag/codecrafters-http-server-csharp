using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using HttpServer.Content;
using Microsoft.Extensions.Logging;

namespace HttpServer;

using RouteHandler = Func<RequestContext, Task<HttpResponse>>;

internal record struct RequestContext
{
    public ClientSession Client;
    public ILogger Log;
    public string RequestPath;
    // TODO: Headers etc 
}

internal class RequestHandler
{
    private const string IndexRoute = "/";
    private const string EchoBaseRoute = "/echo/";
    private const string UserAgentRoute = "/user-agent";
    private const string FilesBaseRoute = "/files/";
    
    private readonly Dictionary<string, RouteHandler> _RouteHandlers;
    private readonly ILogger _Log = FauxLogger.Instance;
    private readonly DirectoryInfo? _FilesRoot;

    public RequestHandler(DirectoryInfo? filesRoot)
    {
        _FilesRoot = filesRoot;

        _RouteHandlers = new Dictionary<string, RouteHandler>
        {
            { IndexRoute, Index },
            { $"{EchoBaseRoute}*", Echo },
            { UserAgentRoute, UserAgent },
            { $"{FilesBaseRoute}*", Files },
        };
    }

    public async Task Handle(ClientSession client)
    {
        try
        {
            var requestLine = await client.ReadLine();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                await client.Send(new HttpResponse(StatusCode.InternalServerError, "(╯°□°）╯︵ ┻━┻"));
                return;
            }
            
            var split = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length < 2)
            {
                await client.Send(new HttpResponse(StatusCode.BadRequest));
                return;
            }

            if (TryMatchPath(split[1], out var handler))
            {
                var ctx = new RequestContext
                {
                    Client = client,
                    Log = FauxLogger.Instance,
                    RequestPath = split[1],
                };

                var response = await handler(ctx);
                await client.Send(response);
                return;
            }
            
            await client.Send(new HttpResponse(StatusCode.NotFound));
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.ConnectionReset)
                _Log.LogInformation("Client disconnected");
            else
                _Log.LogError(ex, "Socket exception occurred");
        }
    }

    private bool TryMatchPath(string path, [MaybeNullWhen(false)] out RouteHandler handler)
    {
        if (_RouteHandlers.TryGetValue(path, out handler))
        {
            return true;
        }
        
        var wildcardRoutes = _RouteHandlers.Keys.Where(k => k.Contains('*'));
        foreach (var route in wildcardRoutes)
        {
            var pattern = route.Replace("*", ".*");
            var regex = new Regex(pattern);
            if (regex.IsMatch(path))
            {
                handler = _RouteHandlers[route];
                return true;
            }
        }
        
        return false;
    }
    
    private Task<HttpResponse> Index(RequestContext ctx)
    {
        return Task.FromResult(new HttpResponse(StatusCode.OK));
    }
    
    private Task<HttpResponse> Echo(RequestContext ctx)
    {
        return Task.FromResult(new HttpResponse(StatusCode.OK)
        {
            Content = new PlainTextContent(ctx.RequestPath[EchoBaseRoute.Length..])
        });
    }

    private Task<HttpResponse> Files(RequestContext ctx)
    {
        if (_FilesRoot == null)
        {
            return Task.FromResult(new HttpResponse(StatusCode.InternalServerError));
        }

        var path = ctx.RequestPath[FilesBaseRoute.Length..].TrimStart('/');
        var filePath = Path.Combine(_FilesRoot.FullName, path);
        _Log.LogInformation(filePath);
        if (File.Exists(filePath))
        {
            return Task.FromResult(new HttpResponse(StatusCode.OK)
            {
                Content = new FileContent(File.OpenRead(filePath))
            });
        }

        return Task.FromResult(new HttpResponse(StatusCode.NotFound));
    }
    
    private async Task<HttpResponse> UserAgent(RequestContext ctx)
    {
        var headers = new List<string>();
        var limiter = 0;
        while (limiter < 10)
        {
            ++limiter;
            var line = await ctx.Client.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }
            headers.Add(line);
        }
                
        var userAgent = headers.FirstOrDefault(h =>h.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            return new HttpResponse(StatusCode.OK)
            {
                Content = new PlainTextContent(userAgent.Replace("User-Agent: ", string.Empty))
            };
        }

        return new HttpResponse(StatusCode.BadRequest);
    }
}