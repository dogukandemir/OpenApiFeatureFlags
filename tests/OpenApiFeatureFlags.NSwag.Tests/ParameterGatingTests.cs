using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Shouldly;

namespace OpenApiFeatureFlags.NSwag.Tests;

/// <summary>
/// Gating an operation parameter.
/// </summary>
/// <remarks>
/// NSwag hands the operation processor a map from <c>ParameterInfo</c> to the generated parameter,
/// so parameter gating works for controller actions and minimal APIs alike: a minimal API's delegate
/// is present as a <c>MethodInfo</c> here, which the built-in ASP.NET Core adapter never has. That
/// difference is deliberate and documented in docs/nswag.md.
/// </remarks>
public sealed class ParameterGatingTests
{
    private static IEnumerable<string?> ParameterNames(JsonDocument document, string path)
    {
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");

        // When the gated parameter was the only one, NSwag omits the parameters array entirely.
        return operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()).ToArray()
            : [];
    }

    [Fact]
    public async Task AGatedQueryParameterIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        ParameterNames(document, "/api/widgets/search").ShouldNotContain("term");
    }

    [Fact]
    public async Task AGatedQueryParameterIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("SearchEnabled"),
            mapControllers: true);

        ParameterNames(document, "/api/widgets/search").ShouldContain("term");
    }

    [Fact]
    public async Task AParameterFlagDoesNotHideItsOperation()
    {
        using var document = await TestDocument.BuildAsync(new FakeFlagSource(), mapControllers: true);

        document.RootElement.GetProperty("paths").TryGetProperty("/api/widgets/search", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AGatedMinimalApiParameterIsRemovedWhenItsFlagIsOff()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource(),
            mapEndpoints: app => app.MapGet("/search", ([OpenApiFeatureFlag("SearchEnabled")] string? term) => "results"));

        ParameterNames(document, "/search").ShouldNotContain("term");
    }

    [Fact]
    public async Task AGatedMinimalApiParameterIsKeptWhenItsFlagIsOn()
    {
        using var document = await TestDocument.BuildAsync(
            new FakeFlagSource("SearchEnabled"),
            mapEndpoints: app => app.MapGet("/search", ([OpenApiFeatureFlag("SearchEnabled")] string? term) => "results"));

        ParameterNames(document, "/search").ShouldContain("term");
    }
}
