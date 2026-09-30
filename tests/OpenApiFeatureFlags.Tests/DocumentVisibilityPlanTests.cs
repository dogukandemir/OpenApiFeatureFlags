using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

public sealed class DocumentVisibilityPlanTests
{
    private static readonly OperationKey Orders = new("get", "/api/orders/{id}");
    private static readonly MemberKey Secret = new(typeof(SampleModel), nameof(SampleModel.Secret));

    [Fact]
    public void NormalisesTheHttpMethodSoEnginesAgree()
    {
        Orders.Method.ShouldBe("GET");
        Orders.ToString().ShouldBe("GET /api/orders/{id}");
    }

    [Fact]
    public void KeysMembersByClrOriginNotBySerialisedName()
    {
        Secret.ToString().ShouldBe("OpenApiFeatureFlags.Tests.DocumentVisibilityPlanTests+SampleModel.Secret");
    }

    [Fact]
    public void RemoveModeHidesGatedElements()
    {
        var plan = Build(DocumentMode.Remove, hidden: true);

        plan.IsOperationHidden(Orders).ShouldBeTrue();
        plan.IsMemberHidden(Secret).ShouldBeTrue();
        plan.IsHidden("GET /api/orders/{id}").ShouldBeTrue();
        plan.FlagsFor("GET /api/orders/{id}").ShouldBe(["checkout"]);
    }

    [Fact]
    public void AnnotateModeNeverHidesAnythingEvenWhenTheDecisionSaysHidden()
    {
        // Mode semantics are enforced by the plan itself, so an adapter bug cannot make an
        // annotate-only configuration destructive.
        var plan = Build(DocumentMode.Annotate, hidden: true);

        plan.IsOperationHidden(Orders).ShouldBeFalse();
        plan.IsMemberHidden(Secret).ShouldBeFalse();
        plan.FlagsFor("GET /api/orders/{id}").ShouldBe(["checkout"]);
    }

    [Fact]
    public void IncludeModeIsAlwaysANoOp()
    {
        var plan = Build(DocumentMode.Include, hidden: true);

        plan.IsNoOp.ShouldBeTrue();
        plan.IsOperationHidden(Orders).ShouldBeFalse();
        plan.Decisions.ShouldBeEmpty();
        plan.DocumentFlags.ShouldBeEmpty();
    }

    [Fact]
    public void NoOpPlanIsShared()
    {
        DocumentVisibilityPlan.NoOp.IsNoOp.ShouldBeTrue();
        DocumentVisibilityPlan.NoOp.Mode.ShouldBe(DocumentMode.Include);
    }

    [Fact]
    public void UnknownElementsAreNotHiddenAndCarryNoFlags()
    {
        var plan = Build(DocumentMode.Remove, hidden: true);

        plan.IsHidden("GET /api/unknown").ShouldBeFalse();
        plan.FlagsFor("GET /api/unknown").ShouldBeEmpty();
        plan.IsHidden(null!).ShouldBeFalse();
        plan.FlagsFor(null!).ShouldBeEmpty();
    }

    [Fact]
    public void RepeatedDecisionsForOneElementAreMergedWithOrSemantics()
    {
        var plan = new DocumentVisibilityPlan(DocumentMode.Remove,
        [
            new DocumentVisibilityDecision(DocumentElementKind.Operation, Orders.ToString(), hidden: false, ["first"]),
            new DocumentVisibilityDecision(DocumentElementKind.Operation, Orders.ToString(), hidden: true, ["second"]),
        ]);

        plan.IsOperationHidden(Orders).ShouldBeTrue();
        plan.FlagsFor(Orders.ToString()).ShouldBe(["first", "second"]);
        plan.Decisions.Count.ShouldBe(1);
    }

    [Fact]
    public void DocumentFlagsAreDistinctAndOrdinalOrdered()
    {
        var plan = new DocumentVisibilityPlan(DocumentMode.Remove,
        [
            new DocumentVisibilityDecision(DocumentElementKind.Operation, "GET /b", hidden: true, ["zeta", "alpha"]),
            new DocumentVisibilityDecision(DocumentElementKind.Operation, "GET /a", hidden: true, ["alpha"]),
        ]);

        plan.DocumentFlags.ShouldBe(["alpha", "zeta"]);
    }

    [Fact]
    public void LogSummaryReportsWhatWasHiddenAndWhichFlagsWereInvolved()
    {
        var plan = Build(DocumentMode.Remove, hidden: true);

        var summary = plan.ToLogSummary();

        summary.ShouldContain("mode Remove");
        summary.ShouldContain("2 hidden");
        summary.ShouldContain("checkout");
    }

    [Fact]
    public void LogSummaryExplainsAnEmptyPlan()
    {
        new DocumentVisibilityPlan(DocumentMode.Remove, [])
            .ToLogSummary()
            .ShouldContain("no gated elements");

        DocumentVisibilityPlan.NoOp.ToLogSummary().ShouldContain("mode Include");
    }

    [Fact]
    public void EmptyFlagNameListsInDecisionsAreLegal()
    {
        var plan = new DocumentVisibilityPlan(DocumentMode.Remove,
        [
            new DocumentVisibilityDecision(DocumentElementKind.Property, Secret.ToString(), hidden: true, flags: null),
        ]);

        plan.IsMemberHidden(Secret).ShouldBeTrue();
        plan.FlagsFor(Secret.ToString()).ShouldBeEmpty();
    }

    private static DocumentVisibilityPlan Build(DocumentMode mode, bool hidden) =>
        new(mode,
        [
            new DocumentVisibilityDecision(DocumentElementKind.Operation, Orders.ToString(), hidden, ["checkout"]),
            new DocumentVisibilityDecision(DocumentElementKind.Property, Secret.ToString(), hidden, ["checkout"]),
        ]);

    private sealed class SampleModel
    {
        public string? Secret { get; set; }
    }
}
