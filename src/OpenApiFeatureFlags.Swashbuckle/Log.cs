using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Source-generated log messages for the Swashbuckle adapter.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags removed the parameter {ParameterName} from operation {OperationKey} because a gating flag is not enabled.")]
    public static partial void ParameterHidden(ILogger logger, string parameterName, OperationKey operationKey);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags could not mark the schema for {MemberName} for removal, so it stays visible in the document.")]
    public static partial void MemberCouldNotBeMarked(ILogger logger, string memberName);

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
