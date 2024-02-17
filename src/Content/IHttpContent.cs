namespace HttpServer.Content;

public interface IHttpContent : IAsyncDisposable
{
    string ContentType { get; }
    long Length { get; }

    Task WriteTo(StreamWriter stream);
}