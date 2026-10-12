using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.NSwag;

/// <summary>
/// Source-generated log messages for the NSwag adapter.
/// </summary>
/// <remarks>
/// Numbered from 301 so the ids stay distinct from the core (1-4), the description-gating messages
/// (103-106) and the ASP.NET Core adapter's messages (201-202).
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Debug,
        Message = "OpenApiFeatureFlags removed the parameter {ParameterName} from operation {OperationKey} because a gating flag is not enabled.")]
    public static partial void ParameterHidden(ILogger logger, string parameterName, OperationKey operationKey);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Warning,
        Message = "OpenApiFeatureFlags could not find a property named {JsonPropertyName} on the generated schema for {TypeName}, so it stays visible in the document.")]
    public static partial void PropertyNotRemovable(ILogger logger, string jsonPropertyName, string typeName);
}
