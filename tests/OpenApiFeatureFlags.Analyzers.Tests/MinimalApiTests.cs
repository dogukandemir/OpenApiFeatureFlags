using OpenApiFeatureFlags.Analyzers;
using Shouldly;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>OAFF2001: gated minimal API parameters under the built-in engine.</summary>
public sealed class MinimalApiTests
{
    [Fact]
    public async Task AMinimalApiParameterAttributeIsReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using OpenApiFeatureFlags;

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/search", ([OpenApiFeatureFlag("InternalSearch")] string? scope) => "results");
            """,
            referenceCore: true,
            referenceAspNetCore: true,
            analyzers: [new MinimalApiParameterAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF2001");
    }

    [Fact]
    public async Task AControllerParameterAttributeIsNotReported()
    {
        // A controller action parameter is gated through the controller path, which this engine
        // supports; only minimal API lambda parameters are ignored.
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class SearchController
            {
                public string Get([OpenApiFeatureFlag("InternalSearch")] string? scope) => "results";
            }
            """,
            referenceCore: true,
            referenceAspNetCore: true,
            analyzers: [new MinimalApiParameterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMinimalApiParameterAttributeIsNotReportedWhenNSwagIsReferenced()
    {
        // The NSwag adapter can gate a minimal API's parameter, so the attribute is not a mistake
        // in that combination.
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using Microsoft.AspNetCore.Builder;
            using OpenApiFeatureFlags;

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/search", ([OpenApiFeatureFlag("InternalSearch")] string? scope) => "results");
            """,
            referenceCore: true,
            referenceAspNetCore: true,
            referenceNSwag: true,
            analyzers: [new MinimalApiParameterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }
}
