using System.Text.Json;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenApiFeatureFlags.FastEndpoints.Tests;

/// <summary>
/// Produces a real OpenAPI document from a real ASP.NET Core application running FastEndpoints.
/// </summary>
/// <remarks>
/// The app registers exactly what docs/fastendpoints.md tells a consumer to register, so the tests
/// hold the guide: the framework's <c>ConfigureOpenApi</c> hook hands the engine's options to this
/// library, and the document is requested from the endpoint that serves it.
/// </remarks>
internal static class TestDocument
{
    public const string Route = "/openapi/v1.json";

    /// <summary>Builds the document served by <see cref="Route"/> for the given flag state.</summary>
    public static async Task<JsonDocument> BuildAsync(
        IFeatureFlagSource flagSource,
        DocumentMode mode = DocumentMode.Remove)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddOpenApiFeatureFlags(flagSource, options =>
        {
            options.Mode = mode;
        });

        builder.Services.AddFastEndpoints()
            .OpenApiDocument(o =>
            {
                o.Title = "FastEndpoints tests";
                o.Version = "1.0";

                o.ConfigureOpenApi = options => options.AddOpenApiFeatureFlagTransformers();
            });

        var app = builder.Build();

        app.UseFastEndpoints();
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
}
