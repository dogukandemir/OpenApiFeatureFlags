using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace OpenApiFeatureFlags.AspNetCore;

/// <summary>
/// Removes a schema property whose gating flag is not enabled.
/// </summary>
/// <remarks>
/// <para>
/// This is where the ASP.NET Core engine is <i>easier</i> than Swashbuckle. The transformer is given
/// the JSON type information for the schema being built, so <see cref="JsonPropertyInfo.Name"/> gives
/// the document's property name directly and <see cref="JsonPropertyInfo.AttributeProvider"/> gives the
/// member the attribute was written on. The property can therefore be removed here and now, instead of
/// being tagged and resolved later by object identity — no reproduction of camel-casing or
/// <c>[JsonPropertyName]</c> rules, because both sides of the match come from the same source.
/// </para>
/// <para>
/// <c>required</c> is cleaned in the same pass, and swept again by
/// <see cref="FeatureFlagDocumentTransformer"/>, because a consumer schema transformer registered after
/// this one could re-add an entry for a property that is already gone.
/// </para>
/// </remarks>
internal sealed class FeatureFlagSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagSchemaTransformer> _logger;

    public FeatureFlagSchemaTransformer(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagSchemaTransformer> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var jsonTypeInfo = context.JsonTypeInfo;

        if (jsonTypeInfo.Properties.Count == 0)
        {
            // A primitive, a collection, or a schema built from something other than a C# object:
            // there is nothing here that an attribute could be attached to.
            return Task.CompletedTask;
        }

        foreach (var property in jsonTypeInfo.Properties)
        {
            if (property.AttributeProvider is not MemberInfo member)
            {
                continue;
            }

            var flags = OpenApiFeatureFlagDiscovery.ForMember(member);

            if (flags.Count == 0)
            {
                continue;
            }

            var hidden = _planner.IsHidden(flags);

            _planner.RecordDecision(new DocumentVisibilityDecision(
                DocumentElementKind.Property,
                new MemberKey(jsonTypeInfo.Type, member.Name).ToString(),
                hidden,
                flags));

            if (hidden)
            {
                RemoveProperty(schema, property.Name, jsonTypeInfo.Type);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops the property from the schema, and its name from <c>required</c>.
    /// </summary>
    /// <remarks>
    /// A missing property is warned about rather than ignored: it means the document's name for the
    /// member is not the one assumed here, which would otherwise silently leave a gated property
    /// visible. That is the one outcome this library exists to prevent.
    /// </remarks>
    private void RemoveProperty(OpenApiSchema schema, string jsonPropertyName, Type type)
    {
        if (schema.Properties?.Remove(jsonPropertyName) != true)
        {
            Log.PropertyNotRemovable(_logger, jsonPropertyName, type.FullName ?? type.Name);
            return;
        }

        schema.Required?.Remove(jsonPropertyName);
    }
}
