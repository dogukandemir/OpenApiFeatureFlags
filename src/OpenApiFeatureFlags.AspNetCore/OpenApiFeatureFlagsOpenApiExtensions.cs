using Microsoft.AspNetCore.OpenApi;
using OpenApiFeatureFlags.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the ASP.NET Core adapter.
/// </summary>
/// <remarks>
/// The namespace is <c>Microsoft.Extensions.DependencyInjection</c> so the method is in scope wherever
/// <c>AddOpenApi</c> is called, which is the same place <c>AddSwaggerGen</c> configures Swashbuckle.
/// It mirrors <c>AddOpenApiFeatureFlagFilters</c> in the Swashbuckle adapter deliberately: switching
/// engines should change one line and nothing else.
/// </remarks>
public static class OpenApiFeatureFlagsOpenApiExtensions
{
    /// <summary>
    /// Registers the OpenApiFeatureFlags transformers.
    /// </summary>
    /// <param name="options">The OpenAPI generation options.</param>
    /// <returns>The same options so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The three are registered together and the order between them does not matter: the engine runs
    /// schema transformers first, then operation transformers, then document transformers, whatever
    /// order they were added in. Ordering <i>is</i> significant relative to your own transformers —
    /// call this <b>after</b> any document transformer that rebuilds <c>paths</c> or <c>tags</c>, or it
    /// will put back what this library removed.
    /// </para>
    /// <para>
    /// Requires the core services: call <c>AddOpenApiFeatureFlags()</c> first, or one of the one-call
    /// helpers such as <c>AddOpenApiFeatureFlagsWithFeatureManagement()</c>.
    /// </para>
    /// </remarks>
    public static OpenApiOptions AddOpenApiFeatureFlagTransformers(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSchemaTransformer<FeatureFlagSchemaTransformer>();
        options.AddOperationTransformer<FeatureFlagOperationTransformer>();
        options.AddDocumentTransformer<FeatureFlagDocumentTransformer>();

        return options;
    }
}
