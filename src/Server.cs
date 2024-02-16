using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

var server = new TcpListener(IPAddress.Any, 4221);
server.Start();

var buffer = new Memory<byte>(new byte[1024], 0, 1024);
while (true)
{
    using var client = await server.AcceptTcpClientAsync();

    try
    {
        NetworkStream clientStream = client.GetStream();
        var bytesRead = await clientStream.ReadAsync(buffer);
        var request = Encoding.ASCII.GetString(buffer.Span[..bytesRead]);
        var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n");
        await clientStream.WriteAsync(response);
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

        break;
    }
}