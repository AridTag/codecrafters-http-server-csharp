using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using HttpServer.Content;
using Microsoft.Extensions.Logging;

namespace HttpServer;

using RouteHandler = Func<RequestContext, Task<HttpResponse>>;
using RouteHandlerMap = Dictionary<string, Func<RequestContext, Task<HttpResponse>>>;

internal enum HttpAction
{
    Get,
    Post
}

internal record struct RequestContext
{
    public ClientSession Client;
    public ILogger Log;
    public string RequestPath;
    public IReadOnlyDictionary<string, string> Headers;
}

internal class RequestHandler
{
    private const string IndexRoute = "/";
    private const string EchoBaseRoute = "/echo/";
    private const string UserAgentRoute = "/user-agent";
    private const string FilesBaseRoute = "/files/";
    
    private readonly Dictionary<HttpAction, RouteHandlerMap> _RouteHandlers;
    private readonly ILogger _Log = FauxLogger.Instance;
    private readonly DirectoryInfo? _FilesRoot;

    public RequestHandler(DirectoryInfo? filesRoot)
    {
        _FilesRoot = filesRoot;

        _RouteHandlers = new Dictionary<HttpAction, RouteHandlerMap>
        {
            {
                HttpAction.Get,
                new RouteHandlerMap
                {
                    { IndexRoute, Index },
                    { $"{EchoBaseRoute}*", Echo },
                    { UserAgentRoute, UserAgent },
                    { $"{FilesBaseRoute}*", Files },
                }
            },

            {
                HttpAction.Post,
                new RouteHandlerMap
                {
                    { $"{FilesBaseRoute}*", PostFiles },
                }
            }
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

            if (Enum.TryParse<HttpAction>(split[0], true, out var action)
                && TryMatchPath(action, split[1], out var handler))
            {
                var ctx = new RequestContext
                {
                    Client = client,
                    Log = FauxLogger.Instance,
                    RequestPath = split[1],
                    Headers = await ReadHeaders(client),
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

    private bool TryMatchPath(HttpAction action, string path, [MaybeNullWhen(false)] out RouteHandler handler)
    {
        if (!_RouteHandlers.TryGetValue(action, out var handlerMap))
        {
            handler = null;
            return false;
        }
        
        if (handlerMap.TryGetValue(path, out handler))
        {
            return true;
        }
        
        var wildcardRoutes = handlerMap.Keys.Where(k => k.Contains('*'));
        foreach (var route in wildcardRoutes)
        {
            var pattern = route.Replace("*", ".*");
            var regex = new Regex(pattern);
            if (regex.IsMatch(path))
            {
                handler = handlerMap[route];
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

    private async Task<HttpResponse> PostFiles(RequestContext ctx)
    {
        if (_FilesRoot == null)
        {
            return new HttpResponse(StatusCode.InternalServerError);
        }

        if (ctx.Headers.TryGetValue("Content-Length", out var contentLengthString)
            && int.TryParse(contentLengthString, out var contentLength))
        {
            var destinationPath = ctx.RequestPath[FilesBaseRoute.Length.GetHashCode()..];
            await using var fileStream = File.OpenWrite(Path.Combine(_FilesRoot.FullName, destinationPath));

            var bytesRead = await ctx.Client.StreamReader.ReadInto(fileStream, contentLength, CancellationToken.None);
            if (bytesRead != contentLength)
            {
                return new HttpResponse(StatusCode.InternalServerError, "Size mismatch");
            }
            
            return new HttpResponse(StatusCode.Created);
        }

        return new HttpResponse(StatusCode.BadRequest);
    }
    
    private Task<HttpResponse> UserAgent(RequestContext ctx)
    {
        if (ctx.Headers.TryGetValue("User-Agent", out var userAgent)
            && !string.IsNullOrWhiteSpace(userAgent))
        {
            return Task.FromResult(new HttpResponse(StatusCode.OK)
            {
                Content = new PlainTextContent(userAgent.Replace("User-Agent: ", string.Empty))
            });
        }

        return Task.FromResult(new HttpResponse(StatusCode.BadRequest));
    }

    private static async Task<Dictionary<string, string>> ReadHeaders(ClientSession client, int maxHeaders = 100)
    {
        var headers = new Dictionary<string, string>();
        var limiter = 0;
        while (limiter < maxHeaders)
        {
            ++limiter;
            var line = await client.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }
            var split = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (split.Length == 2)
            {
                // TODO: This isn't safe
                headers.Add(split[0], split[1]);
            }
        }
        return headers;
    }
}