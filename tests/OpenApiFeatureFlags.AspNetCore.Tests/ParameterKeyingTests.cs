using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>
/// Two actions that each gate a parameter of the same name.
/// </summary>
/// <remarks>
/// <c>scope</c> is the kind of name that repeats across actions, and each one is a different document
/// element. A parameter is not a member of its declaring type, so keying it by type plus name would
/// merge the two into a single decision.
/// </remarks>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    [HttpGet("sales")]
    public IActionResult Sales([OpenApiFeatureFlag("SalesScope")] string? scope) => Ok();

    [HttpGet("stock")]
    public IActionResult Stock([OpenApiFeatureFlag("StockScope")] string? scope) => Ok();
}

public sealed class ParameterKeyingTests
{
    [Fact]
    public async Task ParametersOfTheSameNameOnDifferentActionsAreCountedSeparately()
    {
        var log = new List<string>();

        using var document = await TestDocument.BuildAsync(
            // OrdersController's flags are enabled so that the only hidden elements in the document are
            // the two parameters below. That keeps the expected count exact rather than arithmetic on
            // however many controllers this assembly happens to contain.
            new FakeFlagSource("NewCheckout", "InternalSearch"),
            mapControllers: true,
            logSink: log);

        var summary = log.SingleOrDefault(line =>
            line.Contains("finished the document", StringComparison.Ordinal));

        summary.ShouldNotBeNull();

        // One decision per parameter. Merging them would report a single hidden element, under-count
        // the plan, and make a query about one of them answer for the other.
        summary.ShouldContain("2 gated element(s) hidden");

        // The document itself was already correct - parameters are removed per operation from their
        // ParameterInfo - so this guards the reporting fix against a regression that "hides" them
        // twice and takes an operation with it.
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/api/reports/sales", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/reports/stock", out _).ShouldBeTrue();
    }
}
