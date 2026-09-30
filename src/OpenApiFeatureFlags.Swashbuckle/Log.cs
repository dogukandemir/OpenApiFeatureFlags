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
}
