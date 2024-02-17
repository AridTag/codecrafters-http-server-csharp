namespace HttpServer.Content;

public class FileContent : IHttpContent
{
    private readonly FileStream _FileStream;
    
    public string ContentType => "application/octet-stream";
    public long Length => _FileStream.Length;

    public FileContent(FileStream fileStream)
    {
        _FileStream = fileStream;
    }
    
    public async Task WriteTo(StreamWriter stream)
    {
        await stream.FlushAsync();
        await _FileStream.CopyToAsync(stream.BaseStream);
    }
    
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            await _FileStream.DisposeAsync();
        }
    }
}