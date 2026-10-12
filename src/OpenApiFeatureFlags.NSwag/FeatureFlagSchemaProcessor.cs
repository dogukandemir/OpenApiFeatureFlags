using global::Namotion.Reflection;
using global::NJsonSchema;
using global::NJsonSchema.Generation;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.NSwag;

/// <summary>
/// Removes a schema property whose gating flag is not enabled.
/// </summary>
/// <remarks>
/// <para>
/// The document name of a property is not the CLR name, so it is resolved with the same
/// <c>IReflectionService.GetPropertyName</c> call the generator itself uses. That keeps camel-casing,
/// <c>[JsonPropertyName]</c> and every other naming policy identical on both sides of the match
/// without reproducing any of the rules here.
/// </para>
/// <para>
/// <c>required</c> is cleaned in the same pass, and swept again by
/// <see cref="FeatureFlagDocumentProcessor"/>, because a consumer schema processor registered after
/// this one could re-add an entry for a property that is already gone.
/// </para>
/// </remarks>
internal sealed class FeatureFlagSchemaProcessor : ISchemaProcessor
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagSchemaProcessor> _logger;

    public FeatureFlagSchemaProcessor(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagSchemaProcessor> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public void Process(SchemaProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var schema = context.Schema;
        var contextualType = context.ContextualType;

        if (schema?.Properties is not { Count: > 0 } || contextualType is null)
        {
            // A primitive, a collection, or a schema built from something other than a C# object:
            // there is nothing here that an attribute could be attached to.
            return;
        }

        var type = contextualType.OriginalType;

        foreach (var accessor in Accessors(contextualType))
        {
            if (accessor.MemberInfo is not { } member)
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
                new MemberKey(type, member.Name).ToString(),
                hidden,
                flags));

            if (!hidden)
            {
                continue;
            }

            var jsonName = context.Settings.ReflectionService.GetPropertyName(accessor, context.Settings);

            if (string.IsNullOrEmpty(jsonName))
            {
                Log.PropertyNotRemovable(_logger, member.Name, type.FullName ?? type.Name);
            }
            else
            {
                RemoveProperty(schema, jsonName, type);
            }
        }
    }

    private static IEnumerable<ContextualAccessorInfo> Accessors(ContextualType contextualType) =>
        contextualType.Properties.OfType<ContextualAccessorInfo>()
            .Concat(contextualType.Fields.OfType<ContextualAccessorInfo>());

    /// <summary>Drops the property from the schema, and its name from <c>required</c>.</summary>
    /// <remarks>
    /// A missing property is warned about rather than ignored: it means the document's name for the
    /// member is not the one assumed here, which would otherwise silently leave a gated property
    /// visible. That is the one outcome this library exists to prevent.
    /// </remarks>
    private void RemoveProperty(JsonSchema schema, string jsonPropertyName, Type type)
    {
        if (schema.Properties?.Remove(jsonPropertyName) != true)
        {
            Log.PropertyNotRemovable(_logger, jsonPropertyName, type.FullName ?? type.Name);
            return;
        }

        schema.RequiredProperties?.Remove(jsonPropertyName);
    }
}
