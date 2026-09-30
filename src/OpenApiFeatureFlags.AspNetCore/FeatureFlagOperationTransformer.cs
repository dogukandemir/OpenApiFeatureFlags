using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace OpenApiFeatureFlags.AspNetCore;

/// <summary>
/// Hides or annotates an operation when the controller or the action carries
/// <see cref="OpenApiFeatureFlagAttribute"/>, and hides action parameters that carry it.
/// </summary>
/// <remarks>
/// <para>
/// A transformer receives one operation and cannot reach its path item, so it cannot remove anything.
/// It marks the operation and lets <see cref="FeatureFlagDocumentTransformer"/> remove it once the
/// whole document exists.
/// </para>
/// <para>
/// Two ways in, because the attribute arrives differently for the two kinds of endpoint. A controller
/// action is inspected through its <see cref="MethodInfo"/>, which also carries the controller-level
/// attribute and the parameter attributes. Everything else — minimal APIs — carries the attribute as
/// endpoint metadata, so the metadata is scanned instead. Without the second path a minimal API could
/// only ever be gated by its document, which is not what <c>[OpenApiFeatureFlag]</c> promises.
/// </para>
/// </remarks>
internal sealed class FeatureFlagOperationTransformer : IOpenApiOperationTransformer
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagOperationTransformer> _logger;

    public FeatureFlagOperationTransformer(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagOperationTransformer> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var actionMethod = ActionMethodOf(context);
        var flags = FlagsFor(context, actionMethod);

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

        ApplyParameterGating(operation, context, actionMethod);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the flags governing the operation, from the action method when there is one.
    /// </summary>
    private static IReadOnlyList<string> FlagsFor(
        OpenApiOperationTransformerContext context,
        MethodInfo? actionMethod)
    {
        if (actionMethod is not null)
        {
            return OpenApiFeatureFlagDiscovery.ForOperation(actionMethod);
        }

        // No MethodInfo: a minimal API. The attribute reaches the endpoint as metadata, whether it was
        // applied to the delegate or added with WithMetadata.
        var flags = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var metadata in context.Description.ActionDescriptor.EndpointMetadata)
        {
            if (metadata is OpenApiFeatureFlagAttribute attribute)
            {
                flags.Add(attribute.FlagName);
            }
        }

        return [.. flags];
    }

    private void ApplyParameterGating(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        MethodInfo? actionMethod)
    {
        if (actionMethod is null)
        {
            // A parameter can only be gated through its ParameterInfo, which a minimal API's endpoint
            // metadata does not carry. Recorded in docs/aspnetcore.md rather than guessed at here.
            return;
        }

        OperationKey? operationKey = null;

        foreach (var parameter in actionMethod.GetParameters())
        {
            var flags = OpenApiFeatureFlagDiscovery.ForParameter(parameter);

            if (flags.Count == 0)
            {
                continue;
            }

            var hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Parameter,
                MemberKeyFor(actionMethod, parameter.Name).ToString(),
                hidden,
                flags));

            if (!hidden)
            {
                continue;
            }

            operationKey ??= OperationKeyFor(context);
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
            // The key is passed as a value rather than as an already-formatted string so nothing is
            // evaluated while the Debug level is disabled (CA1873).
            Log.ParameterHidden(_logger, parameterName, operationKey);
        }
    }

    private static MethodInfo? ActionMethodOf(OpenApiOperationTransformerContext context) =>
        context.Description.ActionDescriptor is ControllerActionDescriptor controller
            ? controller.MethodInfo
            : null;

    private static OperationKey OperationKeyFor(OpenApiOperationTransformerContext context)
    {
        var relativePath = context.Description.RelativePath ?? string.Empty;
        var method = context.Description.HttpMethod ?? "GET";

        return new OperationKey(method, "/" + relativePath.TrimStart('/'));
    }

    private static MemberKey MemberKeyFor(MethodInfo actionMethod, string? parameterName) =>
        new(actionMethod.DeclaringType ?? typeof(object), parameterName ?? "<unnamed>");
}
