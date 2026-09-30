using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Hides or annotates an operation when the controller or the action carries
/// <see cref="OpenApiFeatureFlagAttribute"/>, and hides action parameters that carry it.
/// </summary>
/// <remarks>
/// The filter cannot remove an operation from its path item, so it marks the operation and lets
/// <see cref="FeatureFlagOperationPruningDocumentFilter"/> remove it once the whole document exists.
/// </remarks>
internal sealed class FeatureFlagOperationFilter : IOperationFilter
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagOperationFilter> _logger;

    public FeatureFlagOperationFilter(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagOperationFilter> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var flags = OpenApiFeatureFlagDiscovery.ForOperation(context.MethodInfo);

        if (flags.Count > 0)
        {
            var key = OperationKeyFor(context);
            var hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Operation,
                key.ToString(),
                hidden,
                flags));

            if (hidden)
            {
                operation.MarkForRemoval();
            }
            else if (_planner.Mode == DocumentMode.Annotate)
            {
                operation.AnnotateWithFlags(flags);
            }
        }

        ApplyParameterGating(operation, context);
    }

    private void ApplyParameterGating(OpenApiOperation operation, OperationFilterContext context)
    {
        OperationKey? operationKey = null;

        foreach (var parameter in context.MethodInfo.GetParameters())
        {
            var flags = OpenApiFeatureFlagDiscovery.ForParameter(parameter);

            if (flags.Count == 0)
            {
                continue;
            }

            var hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Parameter,
                ParameterKeyFor(context, parameter.Name).ToString(),
                hidden,
                flags));

            if (!hidden)
            {
                continue;
            }

            operationKey ??= OperationKeyFor(context);

            // A parameter is not part of components.schemas, so it can be removed here and now.
            RemoveParameter(operation, parameter.Name, operationKey.Value);
        }
    }

    private void RemoveParameter(OpenApiOperation operation, string? parameterName, OperationKey operationKey)
    {
        if (parameterName is null || operation.Parameters is null)
        {
            return;
        }

        var removed = 0;

        for (var index = operation.Parameters.Count - 1; index >= 0; index--)
        {
            if (string.Equals(operation.Parameters[index].Name, parameterName, StringComparison.Ordinal))
            {
                operation.Parameters.RemoveAt(index);
                removed++;
            }
        }

        if (removed > 0)
        {
            // The key is passed as a value, not as an already-formatted string, so nothing is
            // evaluated when the Debug level is disabled (CA1873).
            Log.ParameterHidden(_logger, parameterName, operationKey);
        }
    }

    private static OperationKey OperationKeyFor(OperationFilterContext context)
    {
        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        var method = context.ApiDescription.HttpMethod ?? "GET";

        return new OperationKey(method, "/" + relativePath.TrimStart('/'));
    }

    private static ParameterKey ParameterKeyFor(OperationFilterContext context, string? parameterName)
    {
        var declaringType = context.MethodInfo.DeclaringType ?? typeof(object);
        return new ParameterKey(declaringType, context.MethodInfo.Name, parameterName ?? "<unnamed>");
    }
}
