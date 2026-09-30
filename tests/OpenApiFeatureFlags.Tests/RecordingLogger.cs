using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.Tests;

/// <summary>Captures log messages so tests can assert on the once-per-document summary line.</summary>
internal sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }

    public IReadOnlyList<string> MessagesAt(LogLevel level) =>
        [.. Entries.Where(entry => entry.Level == level).Select(entry => entry.Message)];
}
