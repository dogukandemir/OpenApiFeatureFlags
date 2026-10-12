using System.Text.Json;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Gating a schema property through the model behind a controller operation.
/// </summary>
/// <remarks>
/// The property name in the document is not the CLR name, so the tests deliberately include a member
/// renamed with <c>[JsonPropertyName]</c>: removing the wrong name would leave the gated property
/// visible, which is the one outcome this library exists to prevent.
/// </remarks>
public sealed class SchemaGatingTests
{
    private static JsonElement PropertiesOf(JsonDocument document, string schemaName) =>
        document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(schemaName)
            .GetProperty("properties");

    [Fact]
    public async Task AGatedPropertyIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var properties = PropertiesOf(document, "Widget");

        properties.TryGetProperty("loyalty_points", out _).ShouldBeFalse();
        properties.TryGetProperty("internalNote", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AGatedPropertyIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("Loyalty", "Secret"),
            mapControllers: true);

        var properties = PropertiesOf(document, "Widget");

        properties.TryGetProperty("loyalty_points", out _).ShouldBeTrue();
        properties.TryGetProperty("internalNote", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task UngatedPropertiesAreUntouched()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var properties = PropertiesOf(document, "Widget");

        properties.TryGetProperty("id", out _).ShouldBeTrue();
        properties.TryGetProperty("price", out _).ShouldBeTrue();
        properties.TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ARequiredEntryForARemovedPropertyIsSweptToo()
    {
        // LoyaltyPoints is [Required] and renamed with [JsonPropertyName]; removing the property but
        // leaving its required entry would publish an unsatisfiable schema.
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var schema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Widget");

        RequiredNames(schema).ShouldNotContain("loyalty_points");
    }

    [Fact]
    public async Task ARequiredEntrySurvivesWhileItsPropertyIsKept()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("Loyalty", "Secret"),
            mapControllers: true);

        var schema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Widget");

        RequiredNames(schema).ShouldContain("loyalty_points");
    }

    [Fact]
    public async Task AModelUsedOnlyByAGatedOperationIsPrunedFromTheDocument()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        schemas.TryGetProperty("HiddenModel", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AModelUsedOnlyByAGatedOperationReturnsWhenTheFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("HiddenFeature"),
            mapControllers: true);

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        schemas.TryGetProperty("HiddenModel", out _).ShouldBeTrue();
    }

    private static IEnumerable<string?> RequiredNames(JsonElement schema) =>
        schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(entry => entry.GetString()).ToArray()
            : [];
}
