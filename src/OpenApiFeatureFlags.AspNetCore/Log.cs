using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.AspNetCore;

/// <summary>
/// Source-generated log messages for the ASP.NET Core adapter.
/// </summary>
/// <remarks>
/// Numbered from 201 so the ids stay distinct from the core (1-4) and from the description-gating
/// messages (103-106) that <see cref="DescriptionGateResolver"/> emits through the adapter's own
/// logger.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags removed the parameter {ParameterName} from operation {OperationKey} because a gating flag is not enabled.")]
    public static partial void ParameterHidden(ILogger logger, string parameterName, OperationKey operationKey);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags could not find a property named {JsonPropertyName} on the generated schema for {TypeName}, so it stays visible in the document.")]
    public static partial void PropertyNotRemovable(ILogger logger, string jsonPropertyName, string typeName);
}
