using OpenApiFeatureFlags.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the Swashbuckle adapter.
/// </summary>
/// <remarks>
/// The namespace is <c>Microsoft.Extensions.DependencyInjection</c> to match Swashbuckle's own
/// <c>SwaggerGenOptionsExtensions</c>, so the method appears alongside <c>SwaggerDoc</c> and
/// <c>AddSecurityDefinition</c> wherever swagger is configured.
/// </remarks>
public static class OpenApiFeatureFlagsSwaggerGenExtensions
{
    /// <summary>
    /// Registers the OpenApiFeatureFlags filters.
    /// </summary>
    /// <param name="options">The Swashbuckle generator options.</param>
    /// <returns>The same options so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Registration order matters and is deliberate.</b> Swashbuckle applies filters in the order
    /// they are registered:
    /// </para>
    /// <list type="number">
    ///   <item><description>operation filter — marks gated operations and removes gated parameters;</description></item>
    ///   <item><description>document filter A — removes the marked operations and properties;</description></item>
    ///   <item><description>description filter — resolves <c>&lt;gate&gt;</c> fragments in text;</description></item>
    ///   <item><description>document filter B — prunes orphaned schemas and tags, and completes the plan;</description></item>
    ///   <item><description>schema filter — marks gated properties.</description></item>
    /// </list>
    /// <para>
    /// Call this method <b>after</b> registering your own filters and document processors. A processor
    /// that clears and rebuilds <c>Paths</c> would otherwise re-add removed operations, and one that
    /// rebuilds <c>Tags</c> would re-add removed tags. Registering this last puts operation pruning
    /// before your processor and tag pruning after it, which is the order the design requires.
    /// </para>
    /// </remarks>
    public static SwaggerGenOptions AddOpenApiFeatureFlagFilters(this SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OperationFilter<FeatureFlagOperationFilter>();
        options.DocumentFilter<FeatureFlagOperationPruningDocumentFilter>();
        options.DocumentFilter<FeatureFlagDescriptionDocumentFilter>();
        options.DocumentFilter<FeatureFlagTagPruningDocumentFilter>();
        options.SchemaFilter<FeatureFlagSchemaFilter>();

        return options;
    }
}
