using Microsoft.Extensions.Logging;

namespace MiniCode.Infrastructure;

/// <summary>
/// Renders every log entry as its category in brackets, then the message —
/// <c>[Tool] read_file</c>, <c>[Result] ...</c> — and nothing else. Written to
/// <see cref="Console.Error"/>, so redirecting stderr is how an operator
/// captures a trace without disturbing the chat transcript on stdout.
/// </summary>
public sealed class BracketLoggerProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new BracketLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class BracketLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Console.Error.WriteLine($"[{category}] {formatter(state, exception)}");
    }
}
