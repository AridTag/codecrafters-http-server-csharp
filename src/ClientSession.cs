using System.Net.Sockets;

namespace HttpServer;

internal sealed class ClientSession : IDisposable
{
    private readonly TcpClient _TcpClient;
    private readonly NetworkStream _Stream;
    private readonly StreamReader _Reader;
    private readonly StreamWriter _Writer;

    public ClientSession(TcpClient tcpClient)
    {
        _TcpClient = tcpClient;
        _Stream = _TcpClient.GetStream();
        _Reader = new StreamReader(_Stream);
        _Writer = new StreamWriter(_Stream);
    }

    public Task<string?> ReadLine()
    {
        return _Reader.ReadLineAsync();
    }

    public void Dispose()
    {
        _Writer.Close();
        _Reader.Close();
        _Stream.Dispose();
        _TcpClient.Dispose();
    }

    public async Task Send(HttpResponse response)
    {
        await _Writer.WriteAsync($"{HttpResponse.Protocol} {(int)response.Status} {response.StatusMessage ?? response.Status.ToString()}\r\n");
        // TODO: Write other headers
        if (response.HasContent)
        {
            await _Writer.WriteAsync($"Content-Type: {response.Content.ContentType}\r\n");
            await _Writer.WriteAsync($"Content-Length: {response.Content.Length}\r\n");
        }
        await _Writer.WriteAsync("\r\n");

        if (response.HasContent)
        {
            await response.Content.WriteTo(_Writer);
        }
    }
}