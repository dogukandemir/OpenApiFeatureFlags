using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace OpenApiFeatureFlags.AspNetCore;

/// <summary>
/// Applies the decisions the other two transformers recorded: removes marked operations, resolves
/// <c>&lt;gate&gt;</c> fragments, and finishes the plan.
/// </summary>
/// <remarks>
/// <para>
/// It runs last, after every schema and operation transformer, which is what makes it the only place
/// with the whole document. It is also the only place that calls
/// <see cref="IDocumentVisibilityPlanner.CompleteDocument"/>, which guarantees exactly one summary log
/// line and one canary check per document.
/// </para>
/// <para>
/// Every action is driven by a marker or by text, so a document with no gated elements passes through
/// untouched — which is what keeps an unconditional API byte-identical to one built without this
/// library at all.
/// </para>
/// </remarks>
internal sealed class FeatureFlagDocumentTransformer : IOpenApiDocumentTransformer
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagDocumentTransformer> _logger;

    public FeatureFlagDocumentTransformer(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagDocumentTransformer> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        RemoveMarkedOperations(document);

        // Gating a sentence is not the same decision as annotating a document, so gates resolve in
        // every mode — and before CompleteDocument, so a fragment's flag counts towards the canary
        // exactly like any other read.
        var resolver = new DescriptionGateResolver(_planner, _logger);
        ResolveDescriptions(document, resolver);

        SweepStaleRequiredEntries(document);

        var plan = _planner.CompleteDocument();

        if (plan.Decisions.Count == 0)
        {
            // Nothing was gated: leave the document exactly as the engine produced it.
            return Task.CompletedTask;
        }

        if (plan.Mode == DocumentMode.Annotate)
        {
            AnnotateDocument(document, plan);
        }
        else if (plan.Mode == DocumentMode.Remove)
        {
            PruneUnreferencedSchemas(document);
            PruneOrphanTags(document);
        }

        return Task.CompletedTask;
    }

    private static void RemoveMarkedOperations(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            return;
        }

        foreach (var path in document.Paths.ToArray())
        {
            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.ToArray())
            {
                if (operation.Value.IsMarkedForRemoval())
                {
                    pathItem.Operations.Remove(operation.Key);
                }
                else
                {
                    operation.Value.StripMarker();
                }
            }

            // An empty path item is still a published path, so it goes too.
            if (pathItem.Operations.Count == 0)
            {
                document.Paths.Remove(path.Key);
            }
        }
    }

    private static void ResolveDescriptions(OpenApiDocument document, DescriptionGateResolver resolver)
    {
        if (document.Info is { } info)
        {
            ApplyGate(resolver, info.Description, value => info.Description = value);
        }

        if (document.Tags is not null)
        {
            foreach (var tag in document.Tags)
            {
                ApplyGate(resolver, tag.Description, value => tag.Description = value);
            }
        }

        foreach (var operation in Operations(document))
        {
            ApplyGate(resolver, operation.Summary, value => operation.Summary = value);
            ApplyGate(resolver, operation.Description, value => operation.Description = value);

            if (operation.Parameters is not null)
            {
                foreach (var parameter in operation.Parameters)
                {
                    if (parameter is OpenApiParameter concrete)
                    {
                        ApplyGate(resolver, concrete.Description, value => concrete.Description = value);
                    }
                }
            }

            if (operation.RequestBody is OpenApiRequestBody requestBody)
            {
                ApplyGate(resolver, requestBody.Description, value => requestBody.Description = value);
            }

            if (operation.Responses is null)
            {
                continue;
            }

            foreach (var response in operation.Responses.Values)
            {
                if (response is OpenApiResponse concrete)
                {
                    ApplyGate(resolver, concrete.Description, value => concrete.Description = value);
                }
            }
        }

        WalkSchemas(
            document,
            onConcrete: schema => ApplyGate(resolver, schema.Description, value => schema.Description = value),
            // A $ref carries its own description override, which is where XML docs on a reference-typed
            // property end up.
            onReference: reference =>
                ApplyGate(resolver, reference.Description, value => reference.Description = value));
    }

    /// <summary>
    /// Rewrites a piece of text only when it actually contains a gate.
    /// </summary>
    /// <remarks>
    /// Assigning unconditionally is <b>not</b> equivalent, and the difference is observable. An
    /// <see cref="OpenApiSchemaReference"/> is written out as a bare <c>$ref</c> while its description
    /// is merely populated, but writing to the property marks it as explicitly set and the description
    /// then appears beside the <c>$ref</c> in the output. That alone broke the guarantee that an
    /// ungated document is byte-identical to one built without this library, so nothing is written
    /// unless <see cref="DescriptionGateResolver.Resolve"/> reports a change.
    /// </remarks>
    private static void ApplyGate(DescriptionGateResolver resolver, string? current, Action<string?> assign)
    {
        if (resolver.Resolve(current) is { } rewritten)
        {
            assign(rewritten);
        }
    }

    /// <summary>
    /// Drops <c>required</c> entries that no longer have a matching property.
    /// </summary>
    /// <remarks>
    /// <see cref="FeatureFlagSchemaTransformer"/> cleans <c>required</c> as it removes each property,
    /// but a consumer schema transformer registered after it runs later still and can add an entry back
    /// — including a literal <see langword="null"/>, which is what
    /// <c>Required.Add(Properties.FirstOrDefault(...).Key)</c> produces once the property is gone.
    /// This is the last pass over the document, so nothing runs after it.
    /// </remarks>
    private static void SweepStaleRequiredEntries(OpenApiDocument document) =>
        WalkSchemas(
            document,
            onConcrete: schema =>
            {
                if (schema.Required is not { Count: > 0 })
                {
                    return;
                }

                foreach (var name in schema.Required
                    .Where(name => string.IsNullOrEmpty(name) || schema.Properties?.ContainsKey(name) != true)
                    .ToArray())
                {
                    schema.Required.Remove(name);
                }
            },
            onReference: _ => { });

    /// <summary>
    /// Publishes the document-level flag map used by portals in <see cref="DocumentMode.Annotate"/>.
    /// </summary>
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
        if (document.Tags is not { Count: > 0 } tags)
        {
            return;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var operation in Operations(document))
        {
            if (operation.Tags is null)
            {
                continue;
            }

            foreach (var tag in operation.Tags)
            {
                if (tag.Name is { Length: > 0 } name)
                {
                    used.Add(name);
                }
            }
        }

        foreach (var orphan in tags.Where(tag => tag.Name is null || !used.Contains(tag.Name)).ToArray())
        {
            tags.Remove(orphan);
        }
    }

    private static IEnumerable<OpenApiOperation> Operations(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            yield break;
        }

        foreach (var path in document.Paths.Values)
        {
            if (path is not OpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                yield return operation;
            }
        }
    }

    /// <summary>
    /// Visits every schema reachable from the document, each exactly once.
    /// </summary>
    /// <remarks>
    /// Both roots are needed. Component schemas are the obvious one, but the engine only moves schemas
    /// into <c>components.schemas</c> <i>after</i> every transformer has run, so at this point a
    /// request or response body can still be an inline schema that a components-only walk would miss.
    /// </remarks>
    private static void WalkSchemas(
        OpenApiDocument document,
        Action<OpenApiSchema> onConcrete,
        Action<OpenApiSchemaReference> onReference)
    {
        var concrete = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);
        var references = new HashSet<OpenApiSchemaReference>(ReferenceEqualityComparer.Instance);

        void Visit(IOpenApiSchema? schema)
        {
            switch (schema)
            {
                case OpenApiSchema value when concrete.Add(value):
                    onConcrete(value);

                    if (value.Properties is { Count: > 0 })
                    {
                        foreach (var property in value.Properties.Values.ToArray())
                        {
                            Visit(property);
                        }
                    }

                    Visit(value.Items);
                    Visit(value.AdditionalProperties);
                    Visit(value.Not);

                    foreach (var nested in AllOf(value))
                    {
                        Visit(nested);
                    }

                    foreach (var nested in OneOf(value))
                    {
                        Visit(nested);
                    }

                    foreach (var nested in AnyOf(value))
                    {
                        Visit(nested);
                    }

                    break;

                case OpenApiSchemaReference reference when references.Add(reference):
                    onReference(reference);
                    break;

                default:
                    break;
            }
        }

        if (document.Components?.Schemas is { Count: > 0 } schemas)
        {
            foreach (var schema in schemas.Values.ToArray())
            {
                Visit(schema);
            }
        }

        foreach (var operation in Operations(document))
        {
            if (operation.Parameters is not null)
            {
                foreach (var parameter in operation.Parameters)
                {
                    Visit(parameter.Schema);
                }
            }

            VisitRequestBody(operation.RequestBody, Visit);

            if (operation.Responses is null)
            {
                continue;
            }

            foreach (var response in operation.Responses.Values)
            {
                VisitResponse(response, Visit);
            }
        }
    }

    private static void VisitRequestBody(IOpenApiRequestBody? requestBody, Action<IOpenApiSchema?> visit)
    {
        if (requestBody?.Content is null)
        {
            return;
        }

        foreach (var mediaType in requestBody.Content.Values)
        {
            visit(mediaType.Schema);
        }
    }

    private static void VisitResponse(IOpenApiResponse? response, Action<IOpenApiSchema?> visit)
    {
        if (response?.Content is null)
        {
            return;
        }

        foreach (var mediaType in response.Content.Values)
        {
            visit(mediaType.Schema);
        }
    }

    private static IEnumerable<IOpenApiSchema> AllOf(OpenApiSchema schema) =>
        schema.AllOf ?? [];

    private static IEnumerable<IOpenApiSchema> OneOf(OpenApiSchema schema) =>
        schema.OneOf ?? [];

    private static IEnumerable<IOpenApiSchema> AnyOf(OpenApiSchema schema) =>
        schema.AnyOf ?? [];

    private static string Serialize(OpenApiDocument document)
    {
        // StringWriter is written to synchronously and IOpenApiWriter exposes no Flush, so the document
        // is complete as soon as SerializeAsV3 returns.
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.SerializeAsV3(new OpenApiJsonWriter(writer));
        return writer.ToString();
    }
}
