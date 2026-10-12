using System.Text.Json;
using Shouldly;

namespace OpenApiFeatureFlags.FastEndpoints.Tests;

/// <summary>
/// Gating FastEndpoints endpoints through the built-in engine adapter.
/// </summary>
public sealed class FastEndpointsGatingTests
{
    [Fact]
    public async Task AGatedOperationIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/control", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/checkout", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task AGatedOperationIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource("NewCheckout"));

        document.RootElement.GetProperty("paths").TryGetProperty("/api/checkout", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AGatedDtoPropertyIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(SchemaNameOfCreateRequest)
            .GetProperty("properties");

        properties.TryGetProperty("loyaltyPoints", out _).ShouldBeFalse();
        properties.TryGetProperty("id", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AGatedDtoPropertyIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource("LoyaltyProgram"));

        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(SchemaNameOfCreateRequest)
            .GetProperty("properties");

        properties.TryGetProperty("loyaltyPoints", out _).ShouldBeTrue();
    }

    // FastEndpoints derives schema names from the DTO's full name with the separators stripped
    // ("Namespace.Class" becomes "NamespaceClass") unless ShortSchemaNames is enabled, so the key is
    // computed rather than spelled out.
    private static string SchemaNameOfCreateRequest =>
        typeof(CreateRequest).FullName!.Replace(".", string.Empty);

    [Fact]
    public async Task AGatedSummaryFragmentIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        var summary = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/search")
            .GetProperty("get")
            .GetProperty("summary")
            .GetString();

        summary.ShouldNotBeNull();
        summary.ShouldNotContain("Beta search notes apply.");
        summary.ShouldContain("Search docs.");
    }

    [Fact]
    public async Task AGatedSummaryFragmentIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource("BetaDocs"));

        var summary = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/search")
            .GetProperty("get")
            .GetProperty("summary")
            .GetString();

        summary.ShouldNotBeNull();
        summary.ShouldContain("Beta search notes apply.");
    }

    [Fact]
    public async Task AClassLevelAttributeDoesNotGateTheEndpoint()
    {
        // A measured boundary rather than an ideal: FastEndpoints does not put the endpoint class's
        // attributes into endpoint metadata, and that metadata is where this engine reads gating from
        // for non-controller endpoints. docs/fastendpoints.md prescribes the metadata route
        // (Options(x => x.WithMetadata(...))). If a FastEndpoints update starts copying class
        // attributes, this test fails and the guide can be simplified.
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        document.RootElement.GetProperty("paths").TryGetProperty("/api/class-attr", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AMethodLevelAttributeDoesNotGateTheEndpoint()
    {
        // Measured on the probe as well: neither the endpoint class nor its members carry their
        // attributes into endpoint metadata, so only the metadata route gates.
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        document.RootElement.GetProperty("paths").TryGetProperty("/api/method-attr", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AGatedGetDtoPropertyLeavesTheQueryParameterInPlace()
    {
        // The other measured boundary: a GET DTO property is flattened into a query parameter, and a
        // parameter can only be removed through the controller path, which FastEndpoints does not
        // have. Gate the operation instead. docs/fastendpoints.md records this.
        using var document = await TestDocument.BuildAsync(new FakeFlagSource());

        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/param-search")
            .GetProperty("get")
            .GetProperty("parameters");

        parameters.EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .ShouldContain("scope");
    }

    [Fact]
    public async Task AnUngatedApplicationIsUntouched()
    {
        // No gates are applied to the control endpoint, so it must serialise identically in both
        // flag states.
        using var off = await TestDocument.BuildAsync(new FakeFlagSource());
        using var on = await TestDocument.BuildAsync(new FakeFlagSource("NewCheckout", "LoyaltyProgram", "BetaDocs", "InternalSearch", "ClassFlag"));

        var controlPaths = off.RootElement.GetProperty("paths").GetProperty("/api/control");
        var controlPathsOn = on.RootElement.GetProperty("paths").GetProperty("/api/control");

        JsonSerializer.Serialize(controlPaths).ShouldBe(JsonSerializer.Serialize(controlPathsOn));
    }
}
