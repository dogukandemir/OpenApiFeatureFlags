using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// An API with no gated elements must come out byte-identical to one built without this library.
/// </summary>
public sealed class RegressionTests
{
    private static void MapUngatedEndpoints(WebApplication app)
    {
        app.MapGet("/plain", () => "x");
        app.MapPost("/plain", () => "y");
        app.MapGet("/plain/{id}", (int id) => id);
    }

    [Fact]
    public async Task AnUngatedApiSerialisesIdenticallyWithAndWithoutTheProcessors()
    {
        using var withProcessors = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: MapUngatedEndpoints);

        using var withoutProcessors = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            useProcessors: false,
            mapEndpoints: MapUngatedEndpoints);

        TestDocument.ToJson(withProcessors).ShouldBe(TestDocument.ToJson(withoutProcessors));
    }

    [Fact]
    public async Task IncludeModeLeavesAGatedApiUntouchedToo()
    {
        // The plan is a no-op in Include mode, so even a gated API must serialise exactly as if the
        // processors were not there.
        void MapGated(WebApplication app) =>
            app.MapGet("/gated", () => "x").WithMetadata(new OpenApiFeatureFlagAttribute("F"));

        using var withProcessors = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mode: DocumentMode.Include,
            mapEndpoints: MapGated);

        using var withoutProcessors = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            useProcessors: false,
            mapEndpoints: MapGated);

        TestDocument.ToJson(withProcessors).ShouldBe(TestDocument.ToJson(withoutProcessors));
    }
}
