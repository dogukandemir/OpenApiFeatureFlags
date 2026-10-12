using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Produces a real OpenAPI document from a real ASP.NET Core application through NSwag's own
/// middleware.
/// </summary>
/// <remarks>
/// The document is obtained the way a consumer obtains it, by asking the endpoint that serves it.
/// That is not a workaround: it means every test runs the processors in the position they occupy in
/// a deployed application, inside NSwag's generation pipeline, and it also proves the wiring recipe
/// from docs/nswag.md (the two-argument <c>AddOpenApiDocument</c> overload) actually works.
/// </remarks>
internal static class TestDocument
{
    public const string Route = "/swagger/v1/swagger.json";

    /// <summary>
    /// Builds the document served by <see cref="Route"/> for the given flag state.
    /// </summary>
    /// <param name="flagSource">The flags to evaluate against.</param>
    /// <param name="mode">The document mode.</param>
    /// <param name="canaryEnabled">Whether the fail-loudly guard is armed.</param>
    /// <param name="useProcessors">
    /// <see langword="false"/> to build the same application without this library's processors, which
    /// is what the byte-identical regression test compares against.
    /// </param>
    /// <param name="mapEndpoints">Registers the endpoints to document.</param>
    /// <param name="mapControllers">
    /// Whether to register MVC. Off by default so the minimal-API tests build a document with no
    /// controller surface; on, every controller in this assembly is discovered, which is fine because
    /// the controller tests assert on their own paths only.
    /// </param>
    /// <param name="logSink">
    /// When supplied, every log message the library emits is appended to it. This is the only way a
    /// test can see the plan, because adapters consume it and never hand it back.
    /// </param>
    /// <param name="description">
    /// Sets the document's info description. Only the description-gate tests pass one, so a gate in a
    /// description cannot influence the byte-identical regression comparison.
    /// </param>
    /// <returns>The parsed document. The caller owns it.</returns>
    public static Task<JsonDocument> BuildAsync(
        IFeatureFlagSource flagSource,
        DocumentMode mode = DocumentMode.Remove,
        bool canaryEnabled = true,
        bool useProcessors = true,
        Action<WebApplication>? mapEndpoints = null,
        bool mapControllers = false,
        ICollection<string>? logSink = null,
        string? description = null) =>
        RunAsync(
            flagSource,
            mode,
            canaryEnabled,
            useProcessors,
            mapEndpoints,
            mapControllers,
            logSink,
            description,
            async client =>
            {
                var json = await client.GetStringAsync(Route).ConfigureAwait(false);
                return JsonDocument.Parse(json);
            });

    /// <summary>
    /// Requests the document endpoint and returns the raw status and body, for the cases where the
    /// request is expected to fail.
    /// </summary>
    public static Task<(HttpStatusCode Status, string Body)> BuildRawAsync(
        IFeatureFlagSource flagSource,
        bool canaryEnabled = true,
        Action<WebApplication>? mapEndpoints = null) =>
        RunAsync(
            flagSource,
            DocumentMode.Remove,
            canaryEnabled,
            useProcessors: true,
            mapEndpoints,
            mapControllers: false,
            logSink: null,
            description: null,
            async client =>
            {
                using var response = await client.GetAsync(Route).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (response.StatusCode, body);
            });

    /// <summary>Serialises a document so two of them can be compared as text.</summary>
    public static string ToJson(JsonDocument document) =>
        JsonSerializer.Serialize(document.RootElement);

    private static async Task<T> RunAsync<T>(
        IFeatureFlagSource flagSource,
        DocumentMode mode,
        bool canaryEnabled,
        bool useProcessors,
        Action<WebApplication>? mapEndpoints,
        bool mapControllers,
        ICollection<string>? logSink,
        string? description,
        Func<HttpClient, Task<T>> read)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        if (logSink is not null)
        {
            builder.Logging.AddProvider(new SinkLoggerProvider(logSink));
        }

        builder.WebHost.UseTestServer();

        builder.Services.AddOpenApiFeatureFlags(flagSource, options =>
        {
            options.Mode = mode;
            options.CanaryEnabled = canaryEnabled;
        });

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApiDocument((settings, services) =>
        {
            settings.Title = "Test document";
            settings.Version = "1.0.0";

            if (description is not null)
            {
                settings.Description = description;
            }

            if (useProcessors)
            {
                settings.AddOpenApiFeatureFlagProcessors(services);
            }
        });

        if (mapControllers)
        {
            builder.Services
                .AddControllers()
                .ConfigureApplicationPartManager(manager =>
                    manager.ApplicationParts.Add(new AssemblyPart(typeof(TestDocument).Assembly)));
        }

        var app = builder.Build();

        mapEndpoints?.Invoke(app);

        if (mapControllers)
        {
            app.MapControllers();
        }

        app.UseOpenApi();

        await app.StartAsync().ConfigureAwait(false);

        try
        {
            using var client = app.GetTestClient();
            return await read(client).ConfigureAwait(false);
        }
        finally
        {
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }
}
