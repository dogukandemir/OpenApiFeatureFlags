using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using OpenApiFeatureFlags;
using OpenApiFeatureFlags.FeatureManagement;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wires OpenApiFeatureFlags to Microsoft.FeatureManagement.
/// </summary>
/// <remarks>
/// The namespace is <c>Microsoft.Extensions.DependencyInjection</c> so the methods appear next to
/// <c>AddFeatureManagement</c>, which is where a consumer is already looking.
/// </remarks>
public static class OpenApiFeatureFlagsFeatureManagementExtensions
{
    /// <summary>
    /// One call that wires everything up: registers Microsoft's feature management, points
    /// OpenApiFeatureFlags at it, and applies the given configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="OpenApiFeatureFlagsOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApiFeatureFlagsWithFeatureManagement();
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenApiFeatureFlagsWithFeatureManagement(
        this IServiceCollection services,
        Action<OpenApiFeatureFlagsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is null)
        {
            services.AddOpenApiFeatureFlags();
        }
        else
        {
            services.AddOpenApiFeatureFlags(configure);
        }

        return services.UseFeatureManagement();
    }

    /// <summary>
    /// Points OpenApiFeatureFlags at Microsoft's <see cref="IFeatureManager"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// builder.Services
    ///     .AddOpenApiFeatureFlags(options =&gt; options.Mode = DocumentMode.Annotate)
    ///     .UseFeatureManagement();
    /// </code>
    /// </example>
    public static IServiceCollection UseFeatureManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFeatureManagement();

        // TryAdd, so an application that registered its own IFeatureFlagSource keeps it.
        services.TryAddSingleton<IFeatureFlagSource, FeatureManagerFlagSource>();

        return services;
    }
}
