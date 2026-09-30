using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Document filter that applies the decisions the other filters recorded: it removes marked
/// operations, drops now-empty path items, prunes marked schema properties and cleans up
/// <c>required</c> lists.
/// </summary>
/// <remarks>
/// <para>
/// It runs <b>before</b> any consumer processor that clears and rebuilds <c>Paths</c>, because such a
/// processor would otherwise re-add the operations that were just removed.
/// </para>
/// <para>
/// Property pruning happens here rather than in the schema filter because this filter runs after
/// <i>every</i> schema filter, so a consumer filter that populates <c>required</c> cannot resurrect a
/// removed property.
/// </para>
/// <para>
/// Every action is driven by a marker, so a document with no gated elements passes through
/// untouched — which is what makes the byte-identical regression test possible.
/// </para>
/// </remarks>
internal sealed class FeatureFlagOperationPruningDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc is null)
        {
            return;
        }

        RemoveMarkedOperations(swaggerDoc);
        PruneMarkedProperties(swaggerDoc);
        StripMarkers(swaggerDoc);
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
            }

            // An empty path item is still a published path, so it goes too.
            if (pathItem.Operations.Count == 0)
            {
                document.Paths.Remove(path.Key);
            }
        }
    }

    private static void PruneMarkedProperties(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }

        var visited = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);

        foreach (var schema in document.Components.Schemas.Values.ToArray())
        {
            PruneSchema(schema, visited);
        }
    }

    private static void PruneSchema(IOpenApiSchema? schema, HashSet<OpenApiSchema> visited)
    {
        if (schema is not OpenApiSchema concrete || !visited.Add(concrete))
        {
            return;
        }

        if (concrete.Properties is { Count: > 0 })
        {
            foreach (var name in concrete.Properties.Keys.ToArray())
            {
                var property = concrete.Properties[name];

                if (property.IsMarkedForRemoval())
                {
                    concrete.Properties.Remove(name);
                }
                else
                {
                    PruneSchema(property, visited);
                }
            }
        }

        RemoveStaleRequiredEntries(concrete);

        PruneSchema(concrete.Items, visited);

        foreach (var nested in concrete.AllOf ?? [])
        {
            PruneSchema(nested, visited);
        }

        foreach (var nested in concrete.OneOf ?? [])
        {
            PruneSchema(nested, visited);
        }

        foreach (var nested in concrete.AnyOf ?? [])
        {
            PruneSchema(nested, visited);
        }

        PruneSchema(concrete.AdditionalProperties, visited);
        PruneSchema(concrete.Not, visited);
    }

    /// <summary>
    /// Drops entries from <c>required</c> that no longer have a matching property.
    /// </summary>
    /// <remarks>
    /// Two things put junk here: removing a property leaves its name behind, and a consumer schema
    /// filter written as <c>Required.Add(properties.FirstOrDefault(...).Key)</c> adds a literal
    /// <see langword="null"/> when the property it wanted is already gone.
    /// </remarks>
    private static void RemoveStaleRequiredEntries(OpenApiSchema schema)
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
    }

    private static void StripMarkers(OpenApiDocument document)
    {
        if (document.Paths is not null)
        {
            foreach (var path in document.Paths.Values)
            {
                if (path is not OpenApiPathItem pathItem || pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    operation.StripMarker();
                }
            }
        }

        if (document.Components?.Schemas is null)
        {
            return;
        }

        var visited = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);

        foreach (var schema in document.Components.Schemas.Values.ToArray())
        {
            StripSchemaMarkers(schema, visited);
        }
    }

    private static void StripSchemaMarkers(IOpenApiSchema? schema, HashSet<OpenApiSchema> visited)
    {
        schema.StripMarker();

        if (schema is not OpenApiSchema concrete || !visited.Add(concrete))
        {
            return;
        }

        if (concrete.Properties is { Count: > 0 })
        {
            foreach (var property in concrete.Properties.Values)
            {
                StripSchemaMarkers(property, visited);
            }
        }
    }
}
