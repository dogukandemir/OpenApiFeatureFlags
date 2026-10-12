using Microsoft.Extensions.Logging;
using NSwag.Generation.AspNetCore;
using OpenApiFeatureFlags;
using OpenApiFeatureFlags.NSwag;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wires the OpenApiFeatureFlags processors into an NSwag document generator.
/// </summary>
/// <remarks>
/// <para>
/// Call it inside the <c>AddOpenApiDocument</c> overload that supplies the application's
/// <see cref="IServiceProvider"/>:
/// </para>
/// <code>
/// builder.Services.AddOpenApiFeatureFlags(flagSource);
/// builder.Services.AddOpenApiDocument((settings, services) =&gt;
///     settings.AddOpenApiFeatureFlagProcessors(services));
/// </code>
/// <para>
/// The provider is needed because NSwag builds its settings instance inside a singleton factory,
/// which is also the moment the container can hand over the shared
/// <see cref="IDocumentVisibilityPlanner"/> and the logger factory. The single-argument
/// <c>AddOpenApiDocument(settings =&gt; ...)</c> overload cannot reach a provider, so use the two
/// argument overload above; NSwag offers it for exactly this kind of DI-aware configuration.
/// </para>
/// <para>
/// Call this method last in your configuration callback. The processors it adds act on what the
/// document already contains, so a consumer processor registered after them could re-add an element
/// that was removed. The same ordering guidance applies to the Swashbuckle and ASP.NET Core
/// adapters. See docs/nswag.md.
/// </para>
/// </remarks>
public static class OpenApiFeatureFlagsNSwagExtensions
{
    /// <summary>
    /// Adds the feature-flag operation, schema and document processors to the generator settings.
    /// </summary>
    /// <param name="settings">The document generator settings to extend.</param>
    /// <param name="serviceProvider">
    /// The application's service provider, supplied by the <c>(settings, services)</c> overload of
    /// <c>AddOpenApiDocument</c>. <c>AddOpenApiFeatureFlags</c> must have been called on it.
    /// </param>
    /// <returns>The same settings, so the call can be chained.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static AspNetCoreOpenApiDocumentGeneratorSettings AddOpenApiFeatureFlagProcessors(
        this AspNetCoreOpenApiDocumentGeneratorSettings settings,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var planner = serviceProvider.GetRequiredService<IDocumentVisibilityPlanner>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        settings.OperationProcessors.Add(new FeatureFlagOperationProcessor(
            planner,
            loggerFactory.CreateLogger<FeatureFlagOperationProcessor>()));

        settings.SchemaSettings.SchemaProcessors.Add(new FeatureFlagSchemaProcessor(
            planner,
            loggerFactory.CreateLogger<FeatureFlagSchemaProcessor>()));

        settings.DocumentProcessors.Add(new FeatureFlagDocumentProcessor(
            planner,
            loggerFactory.CreateLogger<FeatureFlagDocumentProcessor>()));

        return settings;
    }
}
