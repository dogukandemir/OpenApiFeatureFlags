using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>A model with nothing gated on it, for the untouched-document comparison.</summary>
public sealed class PlainModel
{
    public string? Id { get; set; }

    public int Count { get; set; }
}

public sealed class NoGatingRegressionTests
{
    private static void MapPlainEndpoints(WebApplication app)
    {
        app.MapGet("/plain", () => "plain");
        app.MapGet("/models/{id}", (string id) => new PlainModel { Id = id, Count = 1 });
    }

    [Fact]
    public async Task ADocumentWithNoGatingIsIdenticalWithAndWithoutTheTransformers()
    {
        // The property this asserts is the one that makes the library safe to adopt: an API that uses
        // no gating gets exactly the document it would have got without the library installed.
        using var withLibrary = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: MapPlainEndpoints);

        using var withoutLibrary = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            useTransformers: false,
            mapEndpoints: MapPlainEndpoints);

        TestDocument.ToJson(withLibrary).ShouldBe(TestDocument.ToJson(withoutLibrary));
    }
}

public sealed class FailClosedTests
{
    private static void MapGatedEndpoint(WebApplication app) =>
        app.MapGet("/x", () => "x").WithMetadata(new OpenApiFeatureFlagAttribute("FlagX"));

    [Fact]
    public async Task TheCanaryRefusesToServeADocumentWhoseGatedElementsCouldNotBeEvaluated()
    {
        // Failing closed is only safe while the flag store answers. When nothing could be read, a
        // document with gated elements is silently missing released endpoints, so it is not served.
        //
        // The in-memory host surfaces the server's exception to the caller rather than turning it into
        // a 500, which is why the assertion is on the exception type instead of a status code. A
        // deployed host converts the same exception into a 500 response.
        await Should.ThrowAsync<FeatureFlagSourceUnavailableException>(() => TestDocument.BuildAsync(
            new UnavailableFlagSource(),
            mapEndpoints: MapGatedEndpoint));
    }

    [Fact]
    public async Task WithTheCanaryOffAnUnreadableFlagHidesTheElementInstead()
    {
        using var document = await TestDocument.BuildAsync(
            new UnavailableFlagSource(),
            canaryEnabled: false,
            mapEndpoints: MapGatedEndpoint);

        document.RootElement.GetProperty("paths").TryGetProperty("/x", out _).ShouldBeFalse();
    }
}
