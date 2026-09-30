using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>
/// Gating an operation through minimal-API endpoint metadata.
/// </summary>
/// <remarks>
/// A minimal API has no <c>MethodInfo</c>, so the attribute arrives as endpoint metadata instead. This
/// is the path that proves the fallback works: without it a minimal API could not be gated at all.
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

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/orders/checkout", out _).ShouldBeTrue();
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
            mapEndpoints: app =>
            {
                app.MapPost("/orders", () => "created")
                    .WithMetadata(new OpenApiFeatureFlagAttribute("CanCreate"));
            });

        document.RootElement.GetProperty("paths").TryGetProperty("/orders", out _).ShouldBeFalse();
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
    public async Task TheInternalMarkerNeverReachesTheDocument()
    {
        // The marker is how the operation transformer talks to the document transformer, so a leak
        // would be a visible defect rather than an internal one.
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("NewCheckout"),
            mapEndpoints: MapEndpoints);

        TestDocument.ToJson(document).ShouldNotContain("x-openapifeatureflags-remove");
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
                app.MapGet("/both", () => "both")
                    .WithMetadata(new OpenApiFeatureFlagAttribute("FlagA"))
                    .WithMetadata(new OpenApiFeatureFlagAttribute("FlagB"));
            });

        document.RootElement.GetProperty("paths").TryGetProperty("/both", out _).ShouldBeFalse();
    }
}
