using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Gating an operation registered as a minimal API.
/// </summary>
/// <remarks>
/// The attribute arrives through endpoint metadata, either on the delegate itself (which NSwag turns
/// into the endpoint's <c>MethodInfo</c>) or through <c>WithMetadata</c> (which never appears on a
/// <c>MethodInfo</c> and is found by the metadata scan). Both paths are covered here because the
/// adapter supports both.
/// </remarks>
public sealed class OperationGatingTests
{
    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/orders", () => "all");

        app.MapGet("/orders/checkout", () => "checkout")
            .WithMetadata(new OpenApiFeatureFlagAttribute("NewCheckout"));
    }

    [Fact]
    public async Task AGatedOperationIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapEndpoints: MapEndpoints);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/orders", out _).ShouldBeTrue();
        paths.TryGetProperty("/orders/checkout", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AGatedOperationIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("NewCheckout"),
            mapEndpoints: MapEndpoints);

        document.RootElement.GetProperty("paths").TryGetProperty("/orders/checkout", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AnAttributeOnTheDelegateItselfGatesTheOperation()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: app =>
            {
                app.MapGet("/plain", () => "ok");
                app.MapGet("/legacy", [OpenApiFeatureFlag("LegacyEndpoint")] () => "old");
            });

        // The ungated neighbour proves the endpoint was discovered at all, so the absent gated path
        // cannot be a discovery failure.
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/plain", out _).ShouldBeTrue();
        paths.TryGetProperty("/legacy", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task EveryMethodOfAPathIsConsideredSeparately()
    {
        // The same path is gated for POST only, so the path must survive and lose one operation.
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: app =>
            {
                app.MapGet("/orders", () => "all");
                app.MapPost("/orders", () => "created")
                    .WithMetadata(new OpenApiFeatureFlagAttribute("CanCreate"));
            });

        var path = document.RootElement.GetProperty("paths").GetProperty("/orders");

        path.TryGetProperty("get", out _).ShouldBeTrue();
        path.TryGetProperty("post", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task APathWhoseEveryOperationWasRemovedIsRemovedToo()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: app => app
                .MapPost("/orders", () => "created")
                .WithMetadata(new OpenApiFeatureFlagAttribute("CanCreate")));

        // With no operations left, NSwag omits the paths object entirely, so an absent key is the
        // strongest form of "the path is gone".
        if (document.RootElement.TryGetProperty("paths", out var paths))
        {
            paths.TryGetProperty("/orders", out _).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task AnnotateKeepsTheOperationAndPublishesTheFlagThatGatesIt()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mode: DocumentMode.Annotate,
            mapEndpoints: MapEndpoints);

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/orders/checkout")
            .GetProperty("get");

        operation.GetProperty("x-feature-flag")[0].GetString().ShouldBe("NewCheckout");

        // The document-level map is what lets a portal filter the whole document at once.
        document.RootElement
            .GetProperty("x-feature-flag")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .ShouldContain("NewCheckout");
    }

    [Fact]
    public async Task IncludePublishesEverythingAndAnnotatesNothing()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mode: DocumentMode.Include,
            mapEndpoints: MapEndpoints);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/orders/checkout", out var operation).ShouldBeTrue();
        operation.TryGetProperty("x-feature-flag", out _).ShouldBeFalse();
        document.RootElement.TryGetProperty("x-feature-flag", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GatingOneOperationDoesNotTouchItsNeighbours()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: app =>
            {
                app.MapGet("/a", () => "a").WithMetadata(new OpenApiFeatureFlagAttribute("FlagA"));
                app.MapGet("/b", () => "b");
                app.MapGet("/c", () => "c").WithMetadata(new OpenApiFeatureFlagAttribute("FlagC"));
            });

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/a", out _).ShouldBeFalse();
        paths.TryGetProperty("/b", out _).ShouldBeTrue();
        paths.TryGetProperty("/c", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task FlagsOnOneOperationAreAndedSoOneDisabledFlagHidesIt()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("FlagA"),
            mapEndpoints: app =>
            {
                app.MapGet("/plain", () => "ok");
                app.MapGet("/both", () => "both")
                    .WithMetadata(new OpenApiFeatureFlagAttribute("FlagA"))
                    .WithMetadata(new OpenApiFeatureFlagAttribute("FlagB"));
            });

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/plain", out _).ShouldBeTrue();
        paths.TryGetProperty("/both", out _).ShouldBeFalse();
    }
}
