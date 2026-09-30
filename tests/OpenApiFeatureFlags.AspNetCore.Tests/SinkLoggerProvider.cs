using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>
/// Collects the formatted text of every log message, so a test can assert on what the library
/// reported rather than only on the document it produced.
/// </summary>
/// <remarks>
/// The summary line is the only public view of the plan: adapters consume it and the caller never sees
/// <see cref="DocumentVisibilityPlan"/> itself. Asserting on the line is therefore how the plan's
/// counting behaviour is observable from outside, which is exactly what makes it worth testing.
/// </remarks>
internal sealed class SinkLoggerProvider : ILoggerProvider
{
    private readonly ICollection<string> _sink;

    public SinkLoggerProvider(ICollection<string> sink) => _sink = sink;

    public ILogger CreateLogger(string categoryName) => new SinkLogger(_sink);

    public void Dispose()
    {
    }

    private sealed class SinkLogger : ILogger
    {
        private readonly ICollection<string> _sink;

        public SinkLogger(ICollection<string> sink) => _sink = sink;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _sink.Add(formatter(state, exception));
    }
}
