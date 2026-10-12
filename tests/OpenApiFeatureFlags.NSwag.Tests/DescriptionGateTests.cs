using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Resolving <c>&lt;gate&gt;</c> fragments in the text a document publishes.
/// </summary>
/// <remarks>
/// Gating a sentence is not the same decision as hiding an element, so gates resolve in every mode:
/// the fragment disappears while its flag is off and comes back when the flag is enabled, without
/// the element itself being touched.
/// </remarks>
public sealed class DescriptionGateTests
{
    private const string DocumentDescription =
        "Test document. <gate flag=\"BetaDocs\">Beta documentation applies.</gate>";

    [Fact]
    public async Task AGatedDocumentDescriptionFragmentIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            description: DocumentDescription);

        var description = document.RootElement.GetProperty("info").GetProperty("description").GetString();

        description.ShouldNotBeNull();
        description.ShouldNotContain("Beta documentation applies.");
        description.ShouldContain("Test document.");
    }

    [Fact]
    public async Task AGatedDocumentDescriptionFragmentIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("BetaDocs"),
            description: DocumentDescription);

        var description = document.RootElement.GetProperty("info").GetProperty("description").GetString();

        description.ShouldNotBeNull();
        description.ShouldContain("Beta documentation applies.");
    }

    [Fact]
    public async Task AGatedSchemaPropertyDescriptionFragmentIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var description = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Widget")
            .GetProperty("properties")
            .GetProperty("price")
            .GetProperty("description")
            .GetString();

        description.ShouldNotBeNull();
        description.ShouldNotContain("Beta pricing formula in use.");
        description.ShouldContain("Price.");
    }

    [Fact]
    public async Task AGatedSchemaPropertyDescriptionFragmentIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("PriceBeta"),
            mapControllers: true);

        var description = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Widget")
            .GetProperty("properties")
            .GetProperty("price")
            .GetProperty("description")
            .GetString();

        description.ShouldNotBeNull();
        description.ShouldContain("Beta pricing formula in use.");
    }

    [Fact]
    public async Task AGatedOperationSummaryFragmentIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/widgets/documented")
            .GetProperty("get");

        operation.TryGetProperty("summary", out var summaryElement).ShouldBeTrue();

        var summary = summaryElement.GetString();
        summary.ShouldNotBeNull();
        summary.ShouldNotContain("Beta widget notes.");
        summary.ShouldContain("Widget docs.");
    }

    [Fact]
    public async Task AGatedOperationSummaryFragmentIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("BetaDocs"),
            mapControllers: true);

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/widgets/documented")
            .GetProperty("get");

        operation.TryGetProperty("summary", out var summaryElement).ShouldBeTrue();

        var summary = summaryElement.GetString();
        summary.ShouldNotBeNull();
        summary.ShouldContain("Beta widget notes.");
    }
}
