using System.Diagnostics.CodeAnalysis;
using HttpServer.Content;

namespace HttpServer;

public enum StatusCode
{
    OK = 200,
    Created = 201,
    BadRequest = 400,
    NotFound = 404,
    InternalServerError = 500,
}

public struct HttpResponse
{
    public const string Protocol = "HTTP/1.1";
    public StatusCode Status { get; }
    public string? StatusMessage { get; set; }

    public IHttpContent? Content { get; set; }

    [MemberNotNullWhen(true, nameof(Content))]
    public bool HasContent => Content?.Length > 0;

    public HttpResponse(StatusCode status, string? statusMessage = null)
    {
        Status = status;
        StatusMessage = statusMessage;
        Content = null;
    }
}