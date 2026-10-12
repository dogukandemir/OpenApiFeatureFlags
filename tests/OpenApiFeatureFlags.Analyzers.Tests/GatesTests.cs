using OpenApiFeatureFlags.Analyzers;
using Shouldly;

namespace OpenApiFeatureFlags.Analyzers.Tests;

/// <summary>OAFF1001: a gate fragment without a flag attribute (every engine except NSwag).</summary>
public sealed class GatesTests
{
    [Fact]
    public async Task AGateWithoutAFlagIsReported()
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
            analyzers: [new GateWithoutFlagAnalyzer()]);

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("OAFF1001");
    }

    [Fact]
    public async Task AFlaggedGateIsNotReported()
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
            analyzers: [new GateWithoutFlagAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AGateWithoutAFlagIsNotReportedWhenNSwagIsReferenced()
    {
        // Under NSwag the escape rule (OAFF3001) speaks for every literal gate, because the
        // "content is hidden" statement would be wrong there: NSwag drops the tag instead.
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
            analyzers: [new GateWithoutFlagAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task AGateWithoutAFlagIsNotReportedWithoutTheFamily()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            """
            public sealed class Widget
            {
                /// <summary>
                /// The total. <gate>no flag here</gate>
                /// </summary>
                public decimal Total { get; set; }
            }
            """,
            referenceCore: false,
            analyzers: [new GateWithoutFlagAnalyzer()]);

        diagnostics.ShouldBeEmpty();
    }
}
