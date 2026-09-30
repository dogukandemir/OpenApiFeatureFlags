using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Resolves <c>&lt;gate flag="X"&gt;…&lt;/gate&gt;</c> fragments in every piece of text the document
/// carries, so part of a description can be gated by a feature flag.
/// </summary>
/// <remarks>
/// <para>
/// Runs in every mode and in every document, including ones with no
/// <see cref="OpenApiFeatureFlagAttribute"/> at all, because a gate can be the only gating in the
/// document. It is also why the wrapper is always stripped even when nothing is hidden: a raw tag
/// reaching a published description would be a visible defect.
/// </para>
/// <para>
/// It runs after the operation-pruning filter, so text belonging to removed operations is already
/// gone, and before the filter that completes the plan, so flags involved for a fragment count
/// towards the D6 canary like any other read.
/// </para>
/// </remarks>
internal sealed class FeatureFlagDescriptionDocumentFilter : IDocumentFilter
{
    private readonly DescriptionGateResolver _resolver;

    public FeatureFlagDescriptionDocumentFilter(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagDescriptionDocumentFilter> logger)
    {
        _resolver = new DescriptionGateResolver(planner, logger);
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc is null)
        {
            return;
        }

        ResolveDocumentText(swaggerDoc);
        ResolveTagText(swaggerDoc);
        ResolveOperations(swaggerDoc);
        ResolveSchemas(swaggerDoc);
    }

    private string? Resolve(string? value) => _resolver.Resolve(value) ?? value;

    private void ResolveDocumentText(OpenApiDocument document)
    {
        if (document.Info is { } info)
        {
            info.Description = Resolve(info.Description);
        }
    }

    private void ResolveTagText(OpenApiDocument document)
    {
        if (document.Tags is null)
        {
            return;
        }

        foreach (var tag in document.Tags)
        {
            tag.Description = Resolve(tag.Description);
        }
    }

    private void ResolveOperations(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            return;
        }

        foreach (var path in document.Paths.Values)
        {
            if (path is not OpenApiPathItem pathItem || pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Summary = Resolve(operation.Summary);
                operation.Description = Resolve(operation.Description);

                if (operation.Parameters is not null)
                {
                    foreach (var parameter in operation.Parameters)
                    {
                        if (parameter is OpenApiParameter concrete)
                        {
                            concrete.Description = Resolve(concrete.Description);
                        }
                    }
                }

                if (operation.RequestBody is OpenApiRequestBody requestBody)
                {
                    requestBody.Description = Resolve(requestBody.Description);
                }

                if (operation.Responses is null)
                {
                    continue;
                }

                foreach (var response in operation.Responses.Values)
                {
                    if (response is OpenApiResponse concrete)
                    {
                        concrete.Description = Resolve(concrete.Description);
                    }
                }
            }
        }
    }

    private void ResolveSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }

        var visited = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);
        var visitedReferences = new HashSet<OpenApiSchemaReference>(ReferenceEqualityComparer.Instance);

        foreach (var schema in document.Components.Schemas.Values.ToArray())
        {
            ResolveSchema(schema, visited, visitedReferences);
        }
    }

    private void ResolveSchema(
        IOpenApiSchema? schema,
        HashSet<OpenApiSchema> visited,
        HashSet<OpenApiSchemaReference> visitedReferences)
    {
        switch (schema)
        {
            case OpenApiSchema concrete when visited.Add(concrete):
                concrete.Description = Resolve(concrete.Description);

                if (concrete.Properties is { Count: > 0 })
                {
                    foreach (var property in concrete.Properties.Values.ToArray())
                    {
                        ResolveSchema(property, visited, visitedReferences);
                    }
                }

                ResolveSchema(concrete.Items, visited, visitedReferences);
                ResolveSchema(concrete.AdditionalProperties, visited, visitedReferences);
                ResolveSchema(concrete.Not, visited, visitedReferences);

                foreach (var nested in concrete.AllOf ?? [])
                {
                    ResolveSchema(nested, visited, visitedReferences);
                }

                foreach (var nested in concrete.OneOf ?? [])
                {
                    ResolveSchema(nested, visited, visitedReferences);
                }

                foreach (var nested in concrete.AnyOf ?? [])
                {
                    ResolveSchema(nested, visited, visitedReferences);
                }

                break;

            case OpenApiSchemaReference reference when visitedReferences.Add(reference):
                // A $ref carries its own description override, which is where XML docs on a
                // reference-typed property end up.
                reference.Description = Resolve(reference.Description);
                break;

            default:
                break;
        }
    }
}
