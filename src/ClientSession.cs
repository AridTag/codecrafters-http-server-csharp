using System.Net.Sockets;
using System.Text;

namespace HttpServer;

internal sealed class ClientSession : IDisposable
{
    private readonly TcpClient _TcpClient;
    private readonly NetworkStream _Stream;
    private readonly TextReader _Reader;

    public ClientSession(TcpClient tcpClient)
    {
        _TcpClient = tcpClient;
        _Stream = _TcpClient.GetStream();
        _Reader = new StreamReader(_Stream);
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
                await _Stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
                return;
            }

            if (split[1] == "/")
            {
                await _Stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));
                return;
            }
            
            await _Stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n\r\n"));
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
        _Reader.Close();
        _Stream.Dispose();
        _TcpClient.Dispose();
    }
}