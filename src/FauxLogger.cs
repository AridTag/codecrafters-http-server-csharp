using Microsoft.Extensions.Logging;

namespace HttpServer;

internal class FauxLogger : ILogger
{
    private static readonly Lazy<FauxLogger> _Instance = new(() => new FauxLogger(), LazyThreadSafetyMode.PublicationOnly);
    
    public static FauxLogger Instance => _Instance.Value;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine($"[{logLevel}]: {formatter(state, exception)}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
}