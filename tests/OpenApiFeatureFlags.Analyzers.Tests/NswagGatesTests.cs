using OpenApiFeatureFlags.Analyzers;
using Shouldly;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>OAFF3001: literal gates in XML comments under the NSwag adapter.</summary>
public sealed class NswagGatesTests
{
    [Fact]
    public async Task ALiteralGateIsReportedWhenNSwagIsReferenced()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class Widget
            {
                /// <summary>
                /// The total. <gate flag="Beta">Beta pricing applies.</gate>
                /// </summary>
                public decimal Total { get; set; }
            }
            """,
            referenceCore: true,
            referenceNSwag: true,
            analyzers: [new NSwagEscapeGateAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF3001");
    }

    [Fact]
    public async Task AnEscapedGateIsNotReported()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class Widget
            {
                /// <summary>
                /// The total. &lt;gate flag="Beta"&gt;Beta pricing applies.&lt;/gate&gt;
                /// </summary>
                public decimal Total { get; set; }
            }
            """,
            referenceCore: true,
            referenceNSwag: true,
            analyzers: [new NSwagEscapeGateAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ALiteralGateIsNotReportedWithoutNSwag()
    {
        // The other engines read documentation comments as written, so a literal gate works
        // there: no rule may fire.
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class Widget
            {
                /// <summary>
                /// The total. <gate flag="Beta">Beta pricing applies.</gate>
                /// </summary>
                public decimal Total { get; set; }
            }
            """,
            referenceCore: true,
            analyzers: [new NSwagEscapeGateAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnderNSwagOnlyTheEscapeRuleSpeaksForAFlaglessGate()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            using OpenApiFeatureFlags;

            public sealed class Widget
            {
                /// <summary>
                /// The total. <gate>no flag here</gate>
                /// </summary>
                public decimal Total { get; set; }
            }
            """,
            referenceCore: true,
            referenceNSwag: true,
            analyzers: [new GateWithoutFlagAnalyzer(), new NSwagEscapeGateAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF3001");
    }
}
