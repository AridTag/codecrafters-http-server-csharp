using System.Buffers;
using System.Text;

namespace HttpServer;

internal sealed class CustomStreamReader : IAsyncDisposable
{
    private readonly BufferedStream _Stream;
    private byte[] _Silly = new byte[1];

    public CustomStreamReader(Stream stream)
    {
        _Stream = new BufferedStream(stream);
    }

    public async Task<string?> ReadLine()
    {
        StringBuilder stringBuilder = new();

        var buffer = new Memory<byte>(_Silly);

        var readBytes = await _Stream.ReadAsync(buffer, CancellationToken.None);
        if (readBytes > 0)
        {
            byte b = _Silly[0];
            while (b != 0xFF && b != '\n')
            {
                if (b != '\r') // Skip carriage return if present
                {
                    stringBuilder.Append((char)b);
                }
                
                readBytes = await _Stream.ReadAsync(buffer, CancellationToken.None);
                if (readBytes == 0)
                {
                    break;
                }

                b = _Silly[0];
            }
        }

        return stringBuilder.Length > 0 ? stringBuilder.ToString() : null;
    }
    
    public async Task<int> ReadInto(Stream destination, int numBytes, CancellationToken cancellationToken)
    {
        const int SpeedyBufferSize = 81920;
        var buffer = ArrayPool<byte>.Shared.Rent(SpeedyBufferSize);
        try
        {
            var totalRead = 0;
            while (totalRead < numBytes)
            {
                var remaining = numBytes - totalRead;
                var toRead = new Memory<byte>(buffer, 0, Math.Min(remaining, buffer.Length));
                var bytesRead = await _Stream.ReadAsync(toRead, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
                
                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
            }

            return totalRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Close()
    {
        _Stream.Close();
    }

    public async ValueTask DisposeAsync()
    {
        await _Stream.DisposeAsync();
    }
}