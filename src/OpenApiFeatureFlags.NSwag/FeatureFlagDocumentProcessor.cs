using global::NJsonSchema;
using global::NSwag;
using global::NSwag.Generation.Processors;
using global::NSwag.Generation.Processors.Contexts;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.NSwag;

/// <summary>
/// Applies the decisions the other two processors recorded: resolves <c>&lt;gate&gt;</c> fragments,
/// finishes the plan, anonymises nothing, and prunes what removal left behind.
/// </summary>
/// <remarks>
/// <para>
/// It is the only place that calls <see cref="IDocumentVisibilityPlanner.CompleteDocument"/>, which
/// guarantees exactly one summary log line and one canary check per document. Gates resolve in every
/// mode, and before <c>CompleteDocument</c>, so a fragment's flag counts towards the canary exactly
/// like any other read.
/// </para>
/// <para>
/// Every action is driven by decisions, so a document with no gated elements passes through
/// untouched, which is what keeps an unconditional API byte-identical to one built without this
/// library.
/// </para>
/// </remarks>
internal sealed class FeatureFlagDocumentProcessor : IDocumentProcessor
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagDocumentProcessor> _logger;

    public FeatureFlagDocumentProcessor(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagDocumentProcessor> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public void Process(DocumentProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var document = context.Document;

        var resolver = new DescriptionGateResolver(_planner, _logger);
        ResolveDescriptions(document, resolver);

        SweepStaleRequiredEntries(document);

        var plan = _planner.CompleteDocument();

        if (plan.Decisions.Count == 0)
        {
            // Nothing was gated: leave the document exactly as the generator produced it.
            return;
        }

        if (plan.Mode == DocumentMode.Annotate)
        {
            AnnotateDocument(document, plan);
        }
        else if (plan.Mode == DocumentMode.Remove)
        {
            PruneEmptyPaths(document);
            PruneUnreferencedSchemas(document);
            PruneOrphanTags(document);
        }
    }

    private static void ResolveDescriptions(OpenApiDocument document, DescriptionGateResolver resolver)
    {
        if (document.Info is { } info)
        {
            ApplyGate(resolver, info.Description, value => info.Description = value);
        }

        if (document.Tags is { Count: > 0 } tags)
        {
            foreach (var tag in tags)
            {
                ApplyGate(resolver, tag.Description, value => tag.Description = value);
            }
        }

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem is null)
            {
                continue;
            }

            ApplyGate(resolver, pathItem.Summary, value => pathItem.Summary = value);
            ApplyGate(resolver, pathItem.Description, value => pathItem.Description = value);

            foreach (var operation in Operations(pathItem))
            {
                ApplyGate(resolver, operation.Summary, value => operation.Summary = value);
                ApplyGate(resolver, operation.Description, value => operation.Description = value);

                if (operation.Parameters is not null)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        ApplyGate(resolver, parameter.Description, value => parameter.Description = value);
                    }
                }

                var requestBody = operation.RequestBody;

                if (requestBody is not null)
                {
                    ApplyGate(resolver, requestBody.Description, value => requestBody.Description = value);
                }

                if (operation.Responses is null)
                {
                    continue;
                }

                foreach (var response in operation.Responses.Values)
                {
                    if (response is not null)
                    {
                        ApplyGate(resolver, response.Description, value => response.Description = value);
                    }
                }
            }
        }

        WalkSchemas(
            document,
            schema => ApplyGate(resolver, schema.Description, value => schema.Description = value));
    }

    /// <summary>
    /// Rewrites a piece of text only when it actually contains a gate.
    /// </summary>
    /// <remarks>
    /// Assigning unconditionally is not equivalent, and the difference is observable: writing to a
    /// description property marks it as explicitly set and a previously absent description then
    /// appears in the output, which would break the guarantee that an ungated document is
    /// byte-identical to one built without this library.
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
    /// <see cref="FeatureFlagSchemaProcessor"/> cleans <c>required</c> as it removes each property,
    /// but a consumer schema processor registered after it runs later still and can add an entry
    /// back, including for a property that is already gone. This is the last pass over the document,
    /// so nothing runs after it.
    /// </remarks>
    private static void SweepStaleRequiredEntries(OpenApiDocument document) =>
        WalkSchemas(
            document,
            schema =>
            {
                if (schema.RequiredProperties is not { Count: > 0 } required)
                {
                    return;
                }

                foreach (var name in required
                    .Where(name => string.IsNullOrEmpty(name) || schema.Properties?.ContainsKey(name) != true)
                    .ToArray())
                {
                    required.Remove(name);
                }
            });

    /// <summary>
    /// Publishes the document-level flag map used by portals in <see cref="DocumentMode.Annotate"/>.
    /// </summary>
    private static void AnnotateDocument(OpenApiDocument document, DocumentVisibilityPlan plan)
    {
        document.ExtensionData ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        document.ExtensionData[FeatureFlagExtensions.FlagAnnotation] = plan.DocumentFlags.ToArray();
    }

    /// <summary>
    /// Drops path items that no longer carry any operation.
    /// </summary>
    /// <remarks>
    /// The operation processor already keeps gated operations out of the document, so an empty path
    /// item should not occur; the sweep exists because an empty path is still a published path, and
    /// paying for a cheap guard is preferable to publishing one.
    /// </remarks>
    private static void PruneEmptyPaths(OpenApiDocument document)
    {
        foreach (var path in document.Paths.ToArray())
        {
            if (path.Value is { Count: 0 })
            {
                document.Paths.Remove(path.Key);
            }
        }
    }

    /// <summary>
    /// Drops model schemas that nothing references any more.
    /// </summary>
    /// <remarks>
    /// Reachability is computed from the serialised document rather than by walking reference
    /// properties, because a <c>$ref</c> is only ever expressed as that string in the output. Both
    /// reference shapes are checked, since the schema type decides which one the serializer writes.
    /// Passes repeat until the document stops shrinking, because removing one schema can orphan
    /// another.
    /// </remarks>
    private static void PruneUnreferencedSchemas(OpenApiDocument document)
    {
        if (document.Definitions is not { Count: > 0 } definitions)
        {
            return;
        }

        while (true)
        {
            var json = document.ToJson();

            var unreachable = definitions.Keys
                .Where(name => !IsReferenced(json, name))
                .ToArray();

            if (unreachable.Length == 0)
            {
                return;
            }

            foreach (var name in unreachable)
            {
                definitions.Remove(name);
            }
        }
    }

    private static bool IsReferenced(string json, string schemaName) =>
        json.Contains($"#/definitions/{schemaName}\"", StringComparison.Ordinal)
        || json.Contains($"#/components/schemas/{schemaName}\"", StringComparison.Ordinal);

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
                if (!string.IsNullOrEmpty(tag))
                {
                    used.Add(tag);
                }
            }
        }

        foreach (var orphan in tags
            .Where(tag => string.IsNullOrEmpty(tag.Name) || !used.Contains(tag.Name))
            .ToArray())
        {
            tags.Remove(orphan);
        }
    }

    private static IEnumerable<OpenApiOperation> Operations(OpenApiDocument document)
    {
        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem is null)
            {
                continue;
            }

            foreach (var operation in Operations(pathItem))
            {
                yield return operation;
            }
        }
    }

    private static IEnumerable<OpenApiOperation> Operations(OpenApiPathItem pathItem)
    {
        foreach (var pair in pathItem)
        {
            yield return pair.Value;
        }
    }

    /// <summary>
    /// Visits every schema reachable from the document, each exactly once.
    /// </summary>
    /// <remarks>
    /// Both roots are needed. The definitions collection is the obvious one, but an operation's
    /// parameter, request body or response can carry a schema that was generated inline, which a
    /// definitions-only walk would miss.
    /// </remarks>
    private static void WalkSchemas(OpenApiDocument document, Action<JsonSchema> onSchema)
    {
        var visited = new HashSet<JsonSchema>(ReferenceEqualityComparer.Instance);

        void Visit(JsonSchema? schema)
        {
            if (schema is null || !visited.Add(schema))
            {
                return;
            }

            onSchema(schema);

            if (schema.Properties is { Count: > 0 } properties)
            {
                foreach (var property in properties.Values.ToArray())
                {
                    Visit(property);
                }
            }

            Visit(schema.Item);
            Visit(schema.AdditionalPropertiesSchema);
            Visit(schema.Not);
            Visit(schema.Reference);

            VisitAll(schema.AllOf, Visit);
            VisitAll(schema.OneOf, Visit);
            VisitAll(schema.AnyOf, Visit);

            if (schema.Definitions is { Count: > 0 } definitions)
            {
                foreach (var definition in definitions.Values.ToArray())
                {
                    Visit(definition);
                }
            }
        }

        if (document.Definitions is { Count: > 0 } schemas)
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

            VisitMediaTypes(operation.RequestBody?.Content, Visit);

            if (operation.Responses is null)
            {
                continue;
            }

            foreach (var response in operation.Responses.Values)
            {
                VisitMediaTypes(response?.Content, Visit);
            }
        }
    }

    private static void VisitAll(IEnumerable<JsonSchema>? schemas, Action<JsonSchema?> visit)
    {
        if (schemas is null)
        {
            return;
        }

        foreach (var schema in schemas.ToArray())
        {
            visit(schema);
        }
    }

    private static void VisitMediaTypes(IDictionary<string, OpenApiMediaType>? content, Action<JsonSchema?> visit)
    {
        if (content is null)
        {
            return;
        }

        foreach (var mediaType in content.Values.ToArray())
        {
            visit(mediaType.Schema);
        }
    }
}
