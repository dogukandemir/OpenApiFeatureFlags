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
        Message = "OpenApiFeatureFlags finished the document in mode {Mode}: {HiddenCount} gated element(s) hidden, {KeptCount} kept, flags involved {InvolvedFlags}.")]
    public static partial void DocumentGenerated(
        ILogger logger,
        DocumentMode mode,
        int hiddenCount,
        int keptCount,
        IReadOnlyCollection<string> involvedFlags);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags could not evaluate the flag {FlagName}; treating it as disabled so the document fails closed.")]
    public static partial void FlagUnreadable(ILogger logger, string flagName, Exception exception);

    // Event ids 101-106 are shared with the adapters: these are emitted through the adapter's own
    // logger instance, so the category a consumer filters on is still the adapter's. They live here
    // because DescriptionGateResolver emits them and that type is in this assembly.

    [LoggerMessage(
        EventId = 103,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags hid a description fragment gated by the flag {FlagName}.")]
    public static partial void DescriptionFragmentHidden(ILogger logger, string flagName);

    [LoggerMessage(
        EventId = 104,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags found a <gate> element without a usable flag attribute and hid its content, so the document fails closed. Element: {GateElement}")]
    public static partial void GateWithoutFlag(ILogger logger, string gateElement);

    [LoggerMessage(
        EventId = 105,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags found a <gate> element that is never closed, so the rest of the description is treated as gated. Element: {GateElement}")]
    public static partial void GateUnbalanced(ILogger logger, string gateElement);

    [LoggerMessage(
        EventId = 106,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags found gates nested deeper than {MaxDepth} levels and hid the innermost content, so the document fails closed. Nested text length: {Length}")]
    public static partial void GateTooDeep(ILogger logger, int maxDepth, int length);
}
