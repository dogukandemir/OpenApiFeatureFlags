using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenApiFeatureFlags;
using OpenApiFeatureFlags.OpenFeature;
using OpenFeature;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wires OpenApiFeatureFlags to the CNCF OpenFeature standard.
/// </summary>
/// <remarks>
/// The namespace is <c>Microsoft.Extensions.DependencyInjection</c> to match the other integration
/// packages in this family.
/// </remarks>
public static class OpenApiFeatureFlagsOpenFeatureExtensions
{
    /// <summary>
    /// One call that wires everything up: points OpenApiFeatureFlags at the global OpenFeature client
    /// and applies the given configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="OpenApiFeatureFlagsOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApiFeatureFlagsWithOpenFeature();
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenApiFeatureFlagsWithOpenFeature(
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

        return services.UseOpenFeature();
    }

    /// <summary>
    /// Points OpenApiFeatureFlags at the global OpenFeature client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Fluent alternative to <see cref="AddOpenApiFeatureFlagsWithOpenFeature"/>:
    /// <code>
    /// builder.Services
    ///     .AddOpenApiFeatureFlags(options =&gt; options.Mode = DocumentMode.Annotate)
    ///     .UseOpenFeature();
    /// </code>
    /// </remarks>
    public static IServiceCollection UseOpenFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd, so an application that registered its own IFeatureFlagSource keeps it.
        services.TryAddSingleton<IFeatureFlagSource>(provider =>
            new OpenFeatureFlagSource(
                Api.Instance.GetClient(logger: provider.GetService<ILogger<OpenFeatureFlagSource>>())));

        return services;
    }
}
