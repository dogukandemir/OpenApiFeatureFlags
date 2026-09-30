using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OpenApiFeatureFlags;

/// <summary>
/// Registration helpers for OpenApiFeatureFlags.
/// </summary>
public static class OpenApiFeatureFlagsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core services. Use this overload when <see cref="IFeatureFlagSource"/> is registered
    /// elsewhere, for example by an integration package.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddOpenApiFeatureFlags(this IServiceCollection services) =>
        AddCore(services, configure: null);

    /// <summary>
    /// Adds the core services and configures them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration of <see cref="OpenApiFeatureFlagsOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddOpenApiFeatureFlags(
        this IServiceCollection services,
        Action<OpenApiFeatureFlagsOptions> configure) =>
        AddCore(services, configure);

    /// <summary>
    /// Adds the core services together with a flag source.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="flagSource">The flag source to evaluate against.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddOpenApiFeatureFlags(
        this IServiceCollection services,
        IFeatureFlagSource flagSource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(flagSource);

        services.TryAddSingleton<IFeatureFlagSource>(flagSource);

        return AddCore(services, configure: null);
    }

    /// <summary>
    /// Adds the core services together with a flag source, and configures them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="flagSource">The flag source to evaluate against.</param>
    /// <param name="configure">Configuration of <see cref="OpenApiFeatureFlagsOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static IServiceCollection AddOpenApiFeatureFlags(
        this IServiceCollection services,
        IFeatureFlagSource flagSource,
        Action<OpenApiFeatureFlagsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(flagSource);

        services.TryAddSingleton<IFeatureFlagSource>(flagSource);

        return AddCore(services, configure);
    }

    private static IServiceCollection AddCore(
        IServiceCollection services,
        Action<OpenApiFeatureFlagsOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OpenApiFeatureFlagsOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Memoisation is keyed off the request. Registering the accessor is
        // idempotent, so it cannot clash with the application's own call.
        services.AddHttpContextAccessor();

        // Everything is a singleton: document engines build filters once and can only inject
        // singletons into them.
        services.TryAddSingleton<IDocumentVisibilityPlanner, DocumentVisibilityPlanner>();

        return services;
    }
}
