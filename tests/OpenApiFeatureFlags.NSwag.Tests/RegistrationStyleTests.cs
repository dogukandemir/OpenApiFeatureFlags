using System.Text.Json;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// The adapter also serves documents registered through <c>AddSwaggerDocument</c> (Swagger 2), not
/// only <c>AddOpenApiDocument</c> (OpenAPI 3).
/// </summary>
/// <remarks>
/// The difference that matters is the reference shape: a Swagger 2 document references model schemas
/// as <c>#/definitions/...</c> while OpenAPI 3 uses <c>#/components/schemas/...</c>. The orphan sweep
/// reads the serialised document and must recognise both, and these tests hold that promise.
/// </remarks>
public sealed class RegistrationStyleTests
{
    private static JsonElement DefinitionsOf(JsonDocument document) =>
        document.RootElement.GetProperty("definitions");

    [Fact]
    public async Task ASwagger2DocumentIsActuallySwagger2()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapControllers: true,
            useSwagger2: true);

        document.RootElement.GetProperty("swagger").GetString().ShouldBe("2.0");
    }

    [Fact]
    public async Task AGatedActionIsRemovedFromASwagger2Document()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapControllers: true,
            useSwagger2: true);

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/widgets", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/widgets/checkout", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AGatedPropertyIsRemovedFromASwagger2Definition()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapControllers: true,
            useSwagger2: true);

        var properties = DefinitionsOf(document).GetProperty("Widget").GetProperty("properties");

        properties.TryGetProperty("loyalty_points", out _).ShouldBeFalse();
        properties.TryGetProperty("id", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AModelUsedOnlyByAGatedOperationIsPrunedFromASwagger2Document()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapControllers: true,
            useSwagger2: true);

        DefinitionsOf(document).TryGetProperty("HiddenModel", out _).ShouldBeFalse();
    }
}
