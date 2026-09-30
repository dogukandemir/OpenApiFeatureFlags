using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>
/// Produces a real OpenAPI document from a real ASP.NET Core application.
/// </summary>
/// <remarks>
/// The engine exposes no public document provider, so the document can only be obtained through the
/// endpoint that serves it. That is not a workaround: it means every test runs the transformers in the
/// position they occupy in a deployed application, after the engine has built the document and before
/// it is written out.
/// </remarks>
internal static class TestDocument
{
    public const string Route = "/openapi/v1.json";

    /// <summary>
    /// Builds the document served by <see cref="Route"/> for the given flag state.
    /// </summary>
    /// <param name="flagSource">The flags to evaluate against.</param>
    /// <param name="mode">The document mode.</param>
    /// <param name="canaryEnabled">Whether the fail-loudly guard is armed.</param>
    /// <param name="useTransformers">
    /// <see langword="false"/> to build the same application without this library's transformers, which
    /// is what the byte-identical regression test compares against.
    /// </param>
    /// <param name="mapEndpoints">Registers the endpoints to document.</param>
    /// <param name="mapControllers">
    /// Whether to register MVC. Off by default so that the byte-identical regression test can build a
    /// document with no controller surface at all; on, every controller in this assembly is
    /// discovered, which is fine because the controller tests assert on their own path only.
    /// </param>
    /// <param name="logSink">
    /// When supplied, every log message the library emits is appended to it. This is the only way a
    /// test can see the plan, because adapters consume it and never hand it back.
    /// </param>
    /// <returns>The parsed document. The caller owns it.</returns>
    public static async Task<JsonDocument> BuildAsync(
        IFeatureFlagSource flagSource,
        DocumentMode mode = DocumentMode.Remove,
        bool canaryEnabled = true,
        bool useTransformers = true,
        Action<WebApplication>? mapEndpoints = null,
        bool mapControllers = false,
        ICollection<string>? logSink = null)
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

        builder.Services.AddOpenApi(options =>
        {
            if (useTransformers)
            {
                options.AddOpenApiFeatureFlagTransformers();
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

        app.MapOpenApi();

        await app.StartAsync().ConfigureAwait(false);

        try
        {
            using var client = app.GetTestClient();
            var json = await client.GetStringAsync(Route).ConfigureAwait(false);
            return JsonDocument.Parse(json);
        }
        finally
        {
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Serialises a document so two of them can be compared as text.</summary>
    public static string ToJson(JsonDocument document) =>
        JsonSerializer.Serialize(document.RootElement);
}
