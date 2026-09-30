using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>
/// The controller path, which is the one that goes through a <c>MethodInfo</c>.
/// </summary>
/// <remarks>
/// It is a separate path in the adapter from minimal APIs, and it is the only one that can gate a
/// parameter, so it needs its own coverage rather than being inferred from the metadata tests.
/// </remarks>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    /// <summary>
    /// Returns every order.
    /// <gate flag="LoyaltyProgram">Loyalty points are included in the response.</gate>
    /// </summary>
    [HttpGet]
    public IActionResult All() => Ok();

    [OpenApiFeatureFlag("NewCheckout")]
    [HttpPost("checkout")]
    public IActionResult Checkout() => Ok();

    [HttpGet("search")]
    public IActionResult Search([OpenApiFeatureFlag("InternalSearch")] string? scope) => Ok();
}

public sealed class ControllerGatingTests
{
    private static Task<System.Text.Json.JsonDocument> BuildAsync(
        DocumentMode mode = DocumentMode.Remove,
        params string[] enabled) =>
        TestDocument.BuildAsync(
            new FakeFlagSource(enabled),
            mode: mode,
            mapControllers: true,
            mapEndpoints: _ => { });

    [Fact]
    public async Task AnActionAttributeGatesTheAction()
    {
        using var document = await BuildAsync();

        document.RootElement.GetProperty("paths").TryGetProperty("/api/orders/checkout", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task TheActionComesBackWhenItsFlagIsOn()
    {
        using var document = await BuildAsync(DocumentMode.Remove, "NewCheckout");

        document.RootElement.GetProperty("paths").TryGetProperty("/api/orders/checkout", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AParameterAttributeRemovesJustThatParameter()
    {
        using var document = await BuildAsync();

        var paths = document.RootElement.GetProperty("paths");

        // The operation itself stays: hiding one parameter is not a reason to hide the endpoint.
        paths.TryGetProperty("/api/orders/search", out var path).ShouldBeTrue();

        var operation = path.GetProperty("get");

        // `scope` is the only parameter, so the writer drops the now-empty list entirely rather than
        // emitting `"parameters":[]`. Both shapes mean the parameter is gone, so the assertion is on
        // the names rather than on the node existing.
        if (operation.TryGetProperty("parameters", out var parameters))
        {
            parameters.EnumerateArray()
                .Select(parameter => parameter.GetProperty("name").GetString())
                .ShouldNotContain("scope");
        }
    }

    [Fact]
    public async Task AParameterIsKeptWhenItsFlagIsOn()
    {
        using var document = await BuildAsync(DocumentMode.Remove, "InternalSearch");

        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/orders/search")
            .GetProperty("get")
            .GetProperty("parameters");

        parameters.EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ShouldContain("scope");
    }

    [Fact]
    public async Task AGatedDescriptionFragmentIsHiddenWhenItsFlagIsOff()
    {
        using var document = await BuildAsync();

        var json = TestDocument.ToJson(document);

        json.ShouldNotContain("Loyalty points are included");
        json.ShouldNotContain("<gate");
    }

    [Fact]
    public async Task AGatedDescriptionFragmentIsKeptAndUnwrappedWhenItsFlagIsOn()
    {
        using var document = await BuildAsync(DocumentMode.Remove, "LoyaltyProgram");

        var json = TestDocument.ToJson(document);

        json.ShouldContain("Loyalty points are included");

        // The wrapper itself must never survive: a raw tag in a published description is a defect.
        json.ShouldNotContain("<gate");
        json.ShouldNotContain("</gate>");
    }

    [Fact]
    public async Task TheWrapperIsStrippedEvenInIncludeModeWhereNothingIsHidden()
    {
        using var document = await BuildAsync(DocumentMode.Include);

        var json = TestDocument.ToJson(document);

        json.ShouldContain("Loyalty points are included");
        json.ShouldNotContain("<gate");
    }
}
