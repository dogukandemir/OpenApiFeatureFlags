using System.Reflection;
using Shouldly;
using Xunit;

namespace OpenApiFeatureFlags.Tests;

/// <summary>
/// Attribute discovery, AND semantics, fail-closed behaviour, the canary,
/// memoisation and the once-per-document log line.
/// </summary>
public sealed class DocumentVisibilityPlannerTests
{
    [Fact]
    public void IsNotHiddenWhenEveryFlagIsEnabled()
    {
        var (planner, source) = CreatePlanner(source => source.Enabled("a", "b"));

        planner.IsHidden(["a", "b"]).ShouldBeFalse();
        source.Reads.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void IsHiddenWhenAnyOneFlagIsDisabled()
    {
        // Multiple attributes on one target are ANDed.
        var (planner, _) = CreatePlanner(source => source.Enabled("a").Disabled("b"));

        planner.IsHidden(["a", "b"]).ShouldBeTrue();
    }

    [Fact]
    public void IsHiddenWhenTheFlagIsUnknown()
    {
        var (planner, _) = CreatePlanner(_ => { });

        planner.IsHidden(["never-configured"]).ShouldBeTrue();
    }

    [Fact]
    public void DoesNotConsultTheSourceForAnUngatedElement()
    {
        var (planner, source) = CreatePlanner(_ => { });

        planner.IsHidden([]).ShouldBeFalse();
        source.Reads.ShouldBeEmpty();
    }

    [Fact]
    public void FailsClosedWhenTheSourceThrows()
    {
        // An unreadable flag must hide the surface, never leak it.
        var (planner, _) = CreatePlanner(source => source.Unreadable("broken"));

        planner.IsHidden(["broken"]).ShouldBeTrue();
    }

    [Fact]
    public void LogsAWarningWhenAFlagCannotBeRead()
    {
        var logger = new RecordingLogger<DocumentVisibilityPlanner>();
        var (planner, _) = CreatePlanner(source => source.Unreadable("broken"), logger: logger);

        planner.IsHidden(["broken"]);

        logger.MessagesAt(LogLevel.Warning).ShouldContain(
            message => message.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public void CanaryThrowsWhenNoFlagCouldBeEvaluated()
    {
        // An unreachable source plus fail-closed would silently delete released endpoints.
        var (planner, _) = CreatePlanner(source => source.Unreadable("a", "b"));

        planner.IsHidden(["a"]);
        planner.IsHidden(["b"]);

        Should.Throw<FeatureFlagSourceUnavailableException>(() => planner.CompleteDocument());
    }

    [Fact]
    public void CanaryDoesNotThrowWhenEvenOneFlagResolved()
    {
        var (planner, _) = CreatePlanner(source => source.Unreadable("a").Disabled("b"));

        planner.IsHidden(["a"]);
        planner.IsHidden(["b"]);

        // 'b' answered, so the source is reachable; fail-closed is trustworthy again.
        Should.NotThrow(() => planner.CompleteDocument());
    }

    [Fact]
    public void CanaryCanBeTurnedOff()
    {
        var (planner, _) = CreatePlanner(
            source => source.Unreadable("a"),
            canaryEnabled: false);

        planner.IsHidden(["a"]);

        Should.NotThrow(() => planner.CompleteDocument());
    }

    [Fact]
    public void CanaryIsIrrelevantWhenNothingIsGated()
    {
        var (planner, _) = CreatePlanner(_ => { });

        Should.NotThrow(() => planner.CompleteDocument());
    }

    [Fact]
    public void IncludeModeShortCircuitsBeforeAnyFlagIsRead()
    {
        var (planner, source) = CreatePlanner(source => source.Unreadable("a"), DocumentMode.Include);

        planner.IsHidden(["a"]).ShouldBeFalse();

        var plan = planner.CompleteDocument();

        plan.IsNoOp.ShouldBeTrue();
        source.Reads.ShouldBeEmpty();
    }

    [Fact]
    public void IncludeModeDoesNotTripTheCanaryEvenWithABrokenSource()
    {
        var (planner, _) = CreatePlanner(source => source.Unreadable("a"), DocumentMode.Include);

        planner.IsHidden(["a"]);

        Should.NotThrow(() => planner.CompleteDocument());
    }

    [Fact]
    public void AnnotateModeNeverHidesAndNeverReadsFlags()
    {
        // Annotate only needs the flag *names*, so an unreachable source cannot damage the document.
        var (planner, source) = CreatePlanner(source => source.Unreadable("a"), DocumentMode.Annotate);

        planner.IsHidden(["a"]).ShouldBeFalse();
        source.Reads.ShouldBeEmpty();
    }

    [Fact]
    public void MemoisesFlagValuesWithinOneRequest()
    {
        var accessor = NewAccessor();
        var (planner, source) = CreatePlanner(source => source.Enabled("a"), httpContextAccessor: accessor);

        for (var i = 0; i < 10; i++)
        {
            planner.IsHidden(["a"]);
        }

        source.Reads.Count.ShouldBe(1);
    }

    [Fact]
    public void MemoisationDoesNotOutliveTheRequest()
    {
        var accessor = NewAccessor();
        var (planner, source) = CreatePlanner(source => source.Enabled("a"), httpContextAccessor: accessor);

        planner.IsHidden(["a"]);
        planner.CompleteDocument();

        // A new request must not see the previous request's snapshot.
        accessor.HttpContext = new DefaultHttpContext();
        planner.IsHidden(["a"]);

        source.Reads.Count.ShouldBe(2);
    }

    [Fact]
    public void MemoisationDoesNotOutliveADocumentWhenThereIsNoRequest()
    {
        // Offline export has no HttpContext; the fallback must still not carry values into the
        // next document.
        var (planner, source) = CreatePlanner(source => source.Enabled("a"));

        planner.IsHidden(["a"]);
        planner.CompleteDocument();

        planner.IsHidden(["a"]);
        planner.CompleteDocument();

        source.Reads.Count.ShouldBe(2);
    }

    [Fact]
    public void RecordsDecisionsAndReportsThemInThePlan()
    {
        var (planner, _) = CreatePlanner(source => source.Enabled("published").Disabled("hidden"));

        planner.RecordDecision(new DocumentVisibilityDecision(
            DocumentElementKind.Operation,
            "GET /api/orders",
            planner.IsHidden(["hidden"]),
            ["hidden"]));

        planner.RecordDecision(new DocumentVisibilityDecision(
            DocumentElementKind.Property,
            "Sample.Order.Total",
            planner.IsHidden(["published"]),
            ["published"]));

        var plan = planner.CompleteDocument();

        plan.Decisions.Count.ShouldBe(2);
        plan.IsOperationHidden(new OperationKey("GET", "/api/orders")).ShouldBeTrue();
        plan.IsHidden("Sample.Order.Total").ShouldBeFalse();
        plan.DocumentFlags.ShouldBe(["hidden", "published"]);
    }

    [Fact]
    public void DocumentStateIsClearedSoTheNextDocumentStartsFresh()
    {
        var (planner, source) = CreatePlanner(source => source.Disabled("a"));

        planner.RecordDecision(new DocumentVisibilityDecision(DocumentElementKind.Operation, "GET /first", true, ["a"]));
        planner.CompleteDocument().Decisions.Count.ShouldBe(1);

        var second = planner.CompleteDocument();

        second.Decisions.ShouldBeEmpty();
        _ = source;
    }

    [Fact]
    public void WritesOneSummaryLinePerDocument()
    {
        var logger = new RecordingLogger<DocumentVisibilityPlanner>();
        var (planner, _) = CreatePlanner(source => source.Enabled("on").Disabled("off"), logger: logger);

        planner.RecordDecision(new DocumentVisibilityDecision(DocumentElementKind.Operation, "GET /one", planner.IsHidden(["off"]), ["off"]));
        planner.RecordDecision(new DocumentVisibilityDecision(DocumentElementKind.Operation, "GET /two", planner.IsHidden(["on"]), ["on"]));

        planner.CompleteDocument();

        var lines = logger.MessagesAt(LogLevel.Information);
        lines.Count.ShouldBe(1);
        lines[0].ShouldContain("Remove");
        lines[0].ShouldContain("1 gated element(s) hidden");
        lines[0].ShouldContain("1 published");
    }

    [Fact]
    public void WritesNothingAboveDebugForADocumentWithNoGatedElements()
    {
        var logger = new RecordingLogger<DocumentVisibilityPlanner>();
        var (planner, _) = CreatePlanner(_ => { }, logger: logger);

        planner.CompleteDocument();

        logger.MessagesAt(LogLevel.Information).ShouldBeEmpty();
        logger.MessagesAt(LogLevel.Debug).Count.ShouldBe(1);
    }

    [Fact]
    public void RejectsNullArguments()
    {
        var (planner, _) = CreatePlanner(_ => { });

        Should.Throw<ArgumentNullException>(() => planner.IsHidden(null!));
        Should.Throw<ArgumentNullException>(() => planner.RecordDecision(null!));
    }

    [Fact]
    public void ConstructorRejectsMissingDependencies()
    {
        var options = Options.Create(new OpenApiFeatureFlagsOptions());

        Should.Throw<ArgumentNullException>(() =>
            new DocumentVisibilityPlanner(null!, options, NullLogger<DocumentVisibilityPlanner>.Instance));
        Should.Throw<ArgumentNullException>(() =>
            new DocumentVisibilityPlanner(new FakeFlagSource(), null!, NullLogger<DocumentVisibilityPlanner>.Instance));
        Should.Throw<ArgumentNullException>(() =>
            new DocumentVisibilityPlanner(new FakeFlagSource(), options, null!));
    }

    [Fact]
    public void TheOfflineFallbackScopeIsNotSharedBetweenThreads()
    {
        // The planner is a singleton and, with no HttpContext, has to park per-document state
        // somewhere. Were that fallback one shared instance, two concurrent offline document
        // generations would share memoised flags and decisions with each other.
        var (planner, source) = CreatePlanner(source => source.Enabled("f"));

        planner.IsFlagEnabled("f").ShouldBeTrue();
        source.Reads.ShouldBe(["f"]);

        Exception? failure = null;
        var other = new Thread(() =>
        {
            try
            {
                planner.IsHidden(["f"]).ShouldBeFalse();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        other.Start();
        other.Join();

        failure.ShouldBeNull();

        // A second thread gets its own scope, so the flag is read again rather than served from the
        // first thread's memoised value.
        source.Reads.Count.ShouldBe(2);
    }

    private static HttpContextAccessor NewAccessor() => new() { HttpContext = new DefaultHttpContext() };

    private static (DocumentVisibilityPlanner Planner, FakeFlagSource Source) CreatePlanner(
        Action<FakeFlagSource> configureSource,
        DocumentMode mode = DocumentMode.Remove,
        bool canaryEnabled = true,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<DocumentVisibilityPlanner>? logger = null)
    {
        var source = new FakeFlagSource();
        configureSource(source);

        var options = Options.Create(new OpenApiFeatureFlagsOptions
        {
            Mode = mode,
            CanaryEnabled = canaryEnabled,
        });

        var planner = new DocumentVisibilityPlanner(
            source,
            options,
            logger ?? NullLogger<DocumentVisibilityPlanner>.Instance,
            httpContextAccessor);

        return (planner, source);
    }
}
