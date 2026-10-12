using System.Reflection;
using global::NSwag;
using global::NSwag.Generation.AspNetCore;
using global::NSwag.Generation.Processors;
using global::NSwag.Generation.Processors.Contexts;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.NSwag;

/// <summary>
/// Drops an operation whose gating flag is not enabled, removes gated parameters, and annotates
/// operations in <see cref="DocumentMode.Annotate"/> mode.
/// </summary>
/// <remarks>
/// <para>
/// NSwag makes operation gating simpler than the other two engines: an operation processor that
/// returns <see langword="false"/> keeps the operation out of the document altogether (verified
/// against the generator source on 2026-10-11), so no marker and no second pass are needed.
/// Parameters are removed from the operation instance directly through the
/// <see cref="OperationProcessorContext.Parameters"/> map, which is the same instance the generator
/// put into the operation.
/// </para>
/// <para>
/// Flags come from two sources and are merged. The <see cref="MethodInfo"/> covers controller
/// actions, including a controller-level attribute, and minimal API delegates. Endpoint metadata
/// covers <c>WithMetadata</c> registrations, which never appear on a <see cref="MethodInfo"/>. Flags
/// that appear only on a parameter are subtracted from the metadata scan so that a gated parameter
/// never hides its whole operation.
/// </para>
/// </remarks>
internal sealed class FeatureFlagOperationProcessor : IOperationProcessor
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagOperationProcessor> _logger;

    public FeatureFlagOperationProcessor(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagOperationProcessor> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public bool Process(OperationProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = context.OperationDescription;
        var operation = description.Operation;
        var actionMethod = context.MethodInfo;

        var flags = FlagsFor(context, actionMethod);
        var hidden = false;

        if (flags.Count > 0)
        {
            hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Operation,
                OperationKeyFor(description).ToString(),
                hidden,
                flags));
        }

        ApplyParameterGating(operation, context, actionMethod);

        if (hidden)
        {
            // Measured behaviour: returning false keeps the operation out of the document.
            return false;
        }

        if (flags.Count > 0 && _planner.Mode == DocumentMode.Annotate)
        {
            operation.AnnotateWithFlags(flags);
        }

        return true;
    }

    private static IReadOnlyList<string> FlagsFor(OperationProcessorContext context, MethodInfo? actionMethod)
    {
        var flags = new SortedSet<string>(StringComparer.Ordinal);

        if (actionMethod is not null)
        {
            foreach (var flag in OpenApiFeatureFlagDiscovery.ForOperation(actionMethod))
            {
                flags.Add(flag);
            }
        }

        var parameterFlags = ParameterFlags(actionMethod);

        foreach (var metadata in EndpointMetadataOf(context))
        {
            if (metadata is OpenApiFeatureFlagAttribute attribute
                && !parameterFlags.Contains(attribute.FlagName))
            {
                flags.Add(attribute.FlagName);
            }
        }

        return [.. flags];
    }

    private static HashSet<string> ParameterFlags(MethodInfo? actionMethod)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (actionMethod is null)
        {
            return names;
        }

        foreach (var parameter in actionMethod.GetParameters())
        {
            foreach (var flag in OpenApiFeatureFlagDiscovery.ForParameter(parameter))
            {
                names.Add(flag);
            }
        }

        return names;
    }

    private static IEnumerable<object> EndpointMetadataOf(OperationProcessorContext context) =>
        context is AspNetCoreOperationProcessorContext aspNetCore
            ? aspNetCore.ApiDescription?.ActionDescriptor?.EndpointMetadata ?? Enumerable.Empty<object>()
            : Enumerable.Empty<object>();

    private void ApplyParameterGating(
        OpenApiOperation operation,
        OperationProcessorContext context,
        MethodInfo? actionMethod)
    {
        if (actionMethod is null || context.Parameters is not { Count: > 0 } parameters)
        {
            return;
        }

        OperationKey? operationKey = null;

        foreach (var pair in parameters)
        {
            var flags = OpenApiFeatureFlagDiscovery.ForParameter(pair.Key);

            if (flags.Count == 0)
            {
                continue;
            }

            var hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Parameter,
                ParameterKeyFor(actionMethod, pair.Key.Name).ToString(),
                hidden,
                flags));

            if (!hidden || operation.Parameters?.Remove(pair.Value) != true)
            {
                continue;
            }

            // The key is passed as a value rather than as an already-formatted string so nothing is
            // evaluated while the Debug level is disabled.
            operationKey ??= OperationKeyFor(context.OperationDescription);
            Log.ParameterHidden(_logger, pair.Key.Name ?? "<unnamed>", operationKey.Value);
        }
    }

    private static OperationKey OperationKeyFor(OpenApiOperationDescription description)
    {
        var path = description.Path is { Length: > 0 } template ? template : "/";

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var method = string.IsNullOrWhiteSpace(description.Method) ? "GET" : description.Method;

        return new OperationKey(method, path);
    }

    private static ParameterKey ParameterKeyFor(MethodInfo actionMethod, string? parameterName) =>
        new(actionMethod.DeclaringType ?? typeof(object), actionMethod.Name, parameterName ?? "<unnamed>");
}
