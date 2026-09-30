using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags;

/// <summary>
/// Source-generated log messages.
/// </summary>
/// <remarks>
/// CA1848 requires the <c>LoggerMessage</c> delegates rather than string-interpolating
/// <see cref="ILogger"/> extensions. Using the source generator additionally gives the
/// <see cref="ILogger.IsEnabled"/> short-circuit for free, which is what CA1873 asks for.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags: mode {Mode}; feature flags ignored for this document.")]
    public static partial void FlagsIgnored(ILogger logger, DocumentMode mode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags: no gated elements in this document (mode {Mode}).")]
    public static partial void NoGatedElements(ILogger logger, DocumentMode mode);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "OpenApiFeatureFlags finished the document in mode {Mode}: {HiddenCount} gated element(s) hidden, {PublishedCount} published, flags involved {InvolvedFlags}.")]
    public static partial void DocumentGenerated(
        ILogger logger,
        DocumentMode mode,
        int hiddenCount,
        int publishedCount,
        IReadOnlyCollection<string> involvedFlags);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags could not evaluate the flag {FlagName}; treating it as disabled so the document fails closed (D5).")]
    public static partial void FlagUnreadable(ILogger logger, string flagName, Exception exception);
}
