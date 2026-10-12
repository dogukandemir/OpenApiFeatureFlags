using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// The canary: what happens when the flag source cannot answer at all.
/// </summary>
public sealed class CanaryTests
{
    [Fact]
    public async Task AFailingFlagSourceFailsTheRequestWhenTheCanaryIsArmed()
    {
        // The in-memory host surfaces the generation exception to the caller rather than turning it
        // into a status code. The contract under test is that no document is served at all.
        var exception = await Should.ThrowAsync<FeatureFlagSourceUnavailableException>(() =>
            TestDocument.BuildRawAsync(
                new UnavailableFlagSource(),
                canaryEnabled: true,
                mapEndpoints: app => app
                    .MapGet("/gated", () => "x")
                    .WithMetadata(new OpenApiFeatureFlagAttribute("F"))));

        exception.Message.ShouldContain("could not evaluate any feature flag");
    }

    [Fact]
    public async Task AFailingFlagSourceHidesGatedElementsWhenTheCanaryIsDisarmed()
    {
        // Fail closed: a store that cannot answer must not leak the gated surface.
        var (status, body) = await TestDocument.BuildRawAsync(
            new UnavailableFlagSource(),
            canaryEnabled: false,
            mapEndpoints: app =>
            {
                app.MapGet("/plain", () => "x");
                app.MapGet("/gated", () => "y").WithMetadata(new OpenApiFeatureFlagAttribute("F"));
            });

        status.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/plain", out _).ShouldBeTrue();
        paths.TryGetProperty("/gated", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ThePlanIsSummarisedOncePerDocument()
    {
        var sink = new List<string>();

        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            logSink: sink,
            mapEndpoints: app => app
                .MapGet("/gated", () => "x")
                .WithMetadata(new OpenApiFeatureFlagAttribute("F")));

        var summaries = sink
            .Where(line => line.Contains("finished the document", StringComparison.Ordinal))
            .ToArray();

        summaries.Length.ShouldBe(1);
        summaries[0].ShouldContain("1 gated element(s) hidden");
    }
}
