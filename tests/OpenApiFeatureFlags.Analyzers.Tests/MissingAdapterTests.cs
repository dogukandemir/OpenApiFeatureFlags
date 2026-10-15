using OpenApiFeatureFlags.Analyzers;
using Shouldly;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>OAFF1002: gated elements in a project whose document engine has no adapter package.</summary>
public sealed class MissingAdapterTests
{
    private const string GatedController =
        """
        using OpenApiFeatureFlags;

        public sealed class OrdersController
        {
            [OpenApiFeatureFlag("NewCheckout")]
            public string Checkout() => "checked out";
        }
        """;

    [Fact]
    public async Task AUsedAttributeWithAnEngineAndNoAdapterIsReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            GatedController,
            referenceCore: true,
            referenceBuiltInEngine: true,
            analyzers: [new MissingAdapterAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF1002");
    }

    [Fact]
    public async Task AUsedAttributeWithTheAdapterReferencedIsNotReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            GatedController,
            referenceCore: true,
            referenceAspNetCore: true,
            referenceBuiltInEngine: true,
            analyzers: [new MissingAdapterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AUsedAttributeWithoutAnyEngineIsNotReported()
    {
        // A contracts library declares the attribute and leaves the adapter to the host, so the
        // rule stays quiet when no engine is in the compilation.
        var diagnostics = await AnalyzerRunner.RunAsync(
            GatedController,
            referenceCore: true,
            analyzers: [new MissingAdapterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEngineWithoutTheAttributeIsNotReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            public sealed class NotGated
            {
                public string Get() => "ok";
            }
            """,
            referenceCore: true,
            referenceBuiltInEngine: true,
            analyzers: [new MissingAdapterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheNSwagEngineWithTheNSwagAdapterIsNotReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            GatedController,
            referenceCore: true,
            referenceNSwag: true,
            referenceNSwagEngine: true,
            analyzers: [new MissingAdapterAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }
}
