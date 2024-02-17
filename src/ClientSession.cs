using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;

namespace HttpServer;

internal sealed class ClientSession : IDisposable
{
    private readonly TcpClient _TcpClient;
    private readonly NetworkStream _Stream;
    private readonly TextReader _Reader;
    private readonly TextWriter _Writer;

    public ClientSession(TcpClient tcpClient)
    {
        _TcpClient = tcpClient;
        _Stream = _TcpClient.GetStream();
        _Reader = new StreamReader(_Stream);
        _Writer = new StreamWriter(_Stream);
    }

    public async Task HandleRequest()
    {
        try
        {
            var requestLine = await _Reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                await _Stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 500 (╯°□°）╯︵ ┻━┻\r\n\r\n"));
                return;
            }
            
            var split = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length < 2)
            {
                await Send(new HttpResponse(StatusCode.BadRequest));
                return;
            }

            if (split[1] == "/")
            {
                await Send(new HttpResponse(StatusCode.OK));
                return;
            }

            const string EchoRoute = "/echo/";
            if (split[1].StartsWith(EchoRoute))
            {
                var response = new HttpResponse(StatusCode.OK)
                {
                    Content = split[1][EchoRoute.Length..]
                };
                await Send(response);
                return;
            }
            
            await Send(new HttpResponse(StatusCode.NotFound));
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                Console.WriteLine("Client severed connection");
            }
            else
            {
                Console.WriteLine(ex.SocketErrorCode);
            }
        }
    }

    public void Dispose()
    {
        _Writer.Close();
        _Reader.Close();
        _Stream.Dispose();
        _TcpClient.Dispose();
    }

    private async Task Send(HttpResponse response)
    {
        await _Writer.WriteAsync($"{HttpResponse.Protocol} {(int)response.Status} {response.Status}\r\n");
        // TODO: Write other headers
        if (response.HasContent)
        {
            await _Writer.WriteAsync("Content-Type: text/plain\r\n");
            await _Writer.WriteAsync($"Content-Length: {response.Content.Length}\r\n");
        }
        await _Writer.WriteAsync("\r\n");

        if (response.HasContent)
        {
            await _Writer.WriteAsync(response.Content);
        }
    }
}

public enum StatusCode
{
    OK = 200,
    BadRequest = 400,
    NotFound = 404,
    InternalServerError = 500,
}

public struct HttpResponse
{
    public const string Protocol = "HTTP/1.1";
    
    private string? _Content;
    
    public StatusCode Status { get; set; }

    public string? Content
    {
        readonly get => _Content;
        set
        {
            _Content = value;
            HasContent = !string.IsNullOrWhiteSpace(_Content);
        }
    }

    [MemberNotNullWhen(true, nameof(Content))]
    public bool HasContent { get; private set; }

    public HttpResponse(StatusCode status)
    {
        Status = status;
        _Content = null;
        HasContent = false;
    }
}