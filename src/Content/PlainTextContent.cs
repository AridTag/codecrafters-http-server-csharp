namespace HttpServer.Content;

public class PlainTextContent : IHttpContent
{
    public string ContentType => "text/plain";

    public string Content { get; }
    
    public long Length => Content.Length;

    public PlainTextContent(string content)
    {
        Content = content;
    }
    
    public Task WriteTo(StreamWriter stream)
    {
        return stream.WriteAsync(Content);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsync(bool disposing)
    {
        return ValueTask.CompletedTask;
    }
}