using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenApiFeatureFlags.Swashbuckle;

/// <summary>
/// Marks a schema property whose gating flag is not enabled, so
/// <see cref="FeatureFlagOperationPruningDocumentFilter"/> can drop it from its parent.
/// </summary>
/// <remarks>
/// The filter cannot remove the property itself: Swashbuckle hands a member-level filter the
/// property's own schema, not the parent's. Tagging the instance and resolving it later by object
/// identity means the adapter never has to reproduce the application's JSON naming rules, which is
/// what makes it safe when a serialiser renames properties.
/// </remarks>
internal sealed class FeatureFlagSchemaFilter : ISchemaFilter
{
    private readonly IDocumentVisibilityPlanner _planner;
    private readonly ILogger<FeatureFlagSchemaFilter> _logger;

    public FeatureFlagSchemaFilter(
        IDocumentVisibilityPlanner planner,
        ILogger<FeatureFlagSchemaFilter> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Parameters are handled by the operation filter, which can actually remove them.
        if (context.MemberInfo is not { } member)
        {
            return;
        }

        var flags = OpenApiFeatureFlagDiscovery.ForMember(member);

        if (flags.Count == 0)
        {
            return;
        }

        var hidden = _planner.IsHidden(flags);

        _planner.RecordDecision(new DocumentVisibilityDecision(
            DocumentElementKind.Property,
            new MemberKey(member.DeclaringType ?? context.Type, member.Name).ToString(),
            hidden,
            flags));

        if (!hidden)
        {
            return;
        }

        if (!schema.TryMarkForRemoval())
        {
            // Better a visible property than a silently dropped one.
            Log.MemberCouldNotBeMarked(_logger, member.Name);
        }
    }
}
