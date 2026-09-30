using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Document filter that finishes the job: it completes the plan, prunes anything the removals
/// orphaned, and cleans up tags.
/// </summary>
/// <remarks>
/// <para>
/// This is "document filter (B)" from the design. It must be registered <b>after</b> any consumer
/// processor that clears and rebuilds <c>Tags</c>, otherwise that processor will re-add the tags this
/// filter just removed (BACKLOG.md 4.2).
/// </para>
/// <para>
/// It is also the only place that calls
/// <see cref="IDocumentVisibilityPlanner.CompleteDocument"/>, which guarantees exactly one summary
/// log line and one canary check per document generation (D6).
/// </para>
/// </remarks>
internal sealed class FeatureFlagTagPruningDocumentFilter : IDocumentFilter
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagTagPruningDocumentFilter> _logger;

    public FeatureFlagTagPruningDocumentFilter(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagTagPruningDocumentFilter> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var plan = _planner.CompleteDocument();

        if (swaggerDoc is null)
        {
            return;
        }

        // Nothing was gated. Leave the document exactly as the engine produced it, so an
        // unconditional API is byte-identical to one built without this library at all.
        if (plan.Decisions.Count == 0)
        {
            return;
        }

        if (plan.Mode == DocumentMode.Annotate)
        {
            AnnotateDocument(swaggerDoc, plan);
            return;
        }

        if (plan.Mode != DocumentMode.Remove)
        {
            return;
        }

        PruneUnreferencedSchemas(swaggerDoc);
        PruneOrphanTags(swaggerDoc);
    }

    /// <summary>
    /// Publishes the document-level flag map used by portals in <see cref="DocumentMode.Annotate"/>.
    /// </summary>
    /// <remarks>
    /// The exact shape is still open (question Q3); today it is an <c>x-feature-flag</c> array of
    /// every flag that governs the document, mirroring the per-operation value.
    /// </remarks>
    private static void AnnotateDocument(OpenApiDocument document, DocumentVisibilityPlan plan)
    {
        var extensions = document.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        extensions[FeatureFlagExtensions.FlagAnnotation] =
            new JsonNodeExtension(FeatureFlagExtensions.ToJsonArray([.. plan.DocumentFlags]));
    }

    /// <summary>
    /// Drops component schemas that nothing references any more.
    /// </summary>
    /// <remarks>
    /// Reachability is computed from the serialised document rather than by walking reference
    /// properties, because a <c>$ref</c> is only ever expressed as that string in the output. Passes
    /// repeat until the document stops shrinking, since removing one schema can orphan another.
    /// </remarks>
    private static void PruneUnreferencedSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
        {
            return;
        }

        while (true)
        {
            var json = Serialize(document);

            var unreachable = schemas.Keys
                .Where(name => !json.Contains($"\"#/components/schemas/{name}\"", StringComparison.Ordinal))
                .ToArray();

            if (unreachable.Length == 0)
            {
                return;
            }

            foreach (var name in unreachable)
            {
                schemas.Remove(name);
            }
        }
    }

    private static void PruneOrphanTags(OpenApiDocument document)
    {
        if (document.Tags is not { Count: > 0 } tags || document.Paths is null)
        {
            return;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in document.Paths.Values)
        {
            if (path is not OpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                var operationTags = operation.Tags;

                if (operationTags is null)
                {
                    continue;
                }

                foreach (var tag in operationTags)
                {
                    if (tag.Name is { Length: > 0 } name)
                    {
                        used.Add(name);
                    }
                }
            }
        }

        foreach (var orphan in tags.Where(tag => tag.Name is null || !used.Contains(tag.Name)).ToArray())
        {
            tags.Remove(orphan);
        }
    }

    private static string Serialize(OpenApiDocument document)
    {
        // StringWriter is written to synchronously and IOpenApiWriter exposes no Flush, so the
        // document is complete as soon as SerializeAsV3 returns.
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiJsonWriter(writer));
        return writer.ToString();
    }
}
