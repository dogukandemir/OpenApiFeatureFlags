using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenApiFeatureFlags;

/// <summary>
/// Default <see cref="IDocumentVisibilityPlanner"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton because document engines construct their filters once and can only
/// inject singletons (see BACKLOG.md 4.1). All per-document state therefore lives in
/// <see cref="RequestScope"/>.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The disposable field is a ThreadLocal holding managed state only, and this type is a process-lifetime singleton, so there is no scope in which disposing it would run. Disposing it at container shutdown would free per-thread slots that the process is about to release anyway. Implementing IDisposable would add public surface and a lifecycle obligation that callers cannot honour meaningfully.")]
public sealed class DocumentVisibilityPlanner : IDocumentVisibilityPlanner
{
    private readonly IFeatureFlagSource _flagSource;
    private readonly IOptions<OpenApiFeatureFlagsOptions> _options;
    private readonly ILogger<DocumentVisibilityPlanner> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // The fallback for the path with no HttpContext (offline export). Per-thread on purpose: this
    // planner is a singleton, so a single shared instance would be mutated by two concurrent
    // document generations at once — a data race on its dictionaries, and a way to publish one
    // document's decisions inside another. Document generation is synchronous, so one generation
    // sees the same scope from the first filter through to the summary line.
    private readonly ThreadLocal<RequestScope> _offlineScope = new(() => new RequestScope());

    /// <summary>
    /// Initialises a new instance of the <see cref="DocumentVisibilityPlanner"/> class.
    /// </summary>
    /// <param name="flagSource">The flag source to evaluate against.</param>
    /// <param name="options">The library options.</param>
    /// <param name="logger">The logger for the once-per-document summary line.</param>
    /// <param name="httpContextAccessor">
    /// Used to scope memoised flag values to the current request. Absent for offline export, where a
    /// single-pass fallback scope is used instead.
    /// </param>
    /// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
    public DocumentVisibilityPlanner(
        IFeatureFlagSource flagSource,
        IOptions<OpenApiFeatureFlagsOptions> options,
        ILogger<DocumentVisibilityPlanner> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(flagSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _flagSource = flagSource;
        _options = options;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public DocumentMode Mode => _options.Value.Mode;

    /// <inheritdoc />
    public bool IsFlagEnabled(string flagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagName);

        var scope = CurrentScope();

        return scope.TryGetFlag(flagName, out var enabled)
            ? enabled
            : Evaluate(flagName, scope);
    }

    /// <inheritdoc />
    public bool IsHidden(IReadOnlyList<string> flagNames)
    {
        ArgumentNullException.ThrowIfNull(flagNames);

        if (flagNames.Count == 0 || _options.Value.Mode != DocumentMode.Remove)
        {
            return false;
        }

        foreach (var flagName in flagNames)
        {
            // AND semantics (D3): one disabled flag is enough to hide the element.
            if (!IsFlagEnabled(flagName))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void RecordDecision(DocumentVisibilityDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        CurrentScope().Add(decision);
    }

    /// <inheritdoc />
    public DocumentVisibilityPlan CompleteDocument()
    {
        var options = _options.Value;
        var httpContext = _httpContextAccessor?.HttpContext;
        var scope = RequestScope.For(httpContext, OfflineScope);

        // Flag values are memoised for the request, but the offline fallback scope outlives a single
        // document, so it must not retain them.
        var keepMemoisedFlags = httpContext is not null;

        if (options.Mode == DocumentMode.Include)
        {
            scope.BuildPlanAndReset(options.Mode, keepMemoisedFlags);
            Log.FlagsIgnored(_logger, options.Mode);
            return DocumentVisibilityPlan.NoOp;
        }

        if (options.CanaryEnabled && scope.AttemptedEvaluations > 0 && scope.SuccessfulEvaluations == 0)
        {
            // D6: failing closed is only safe while the flag source actually answers. If every read
            // failed, publishing would silently drop released endpoints.
            throw new FeatureFlagSourceUnavailableException(scope.AttemptedEvaluations);
        }

        var plan = scope.BuildPlanAndReset(options.Mode, keepMemoisedFlags);
        LogPlan(plan);
        return plan;
    }

    private RequestScope CurrentScope() => RequestScope.For(_httpContextAccessor?.HttpContext, OfflineScope);

    /// <summary>
    /// Gets the scope used when there is no <see cref="HttpContext"/>. See the field comment for why
    /// it is per-thread.
    /// </summary>
    private RequestScope OfflineScope => _offlineScope.Value!;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Fail-closed (D5) is a stated invariant: any failure to read a flag must hide the element, never leak it.")]
    private bool Evaluate(string flagName, RequestScope scope)
    {
        scope.CountAttempt();

        bool enabled;
        try
        {
            enabled = _flagSource.IsEnabled(flagName);
            scope.CountSuccess();
        }
        catch (Exception exception)
        {
            Log.FlagUnreadable(_logger, flagName, exception);
            enabled = false;
        }

        scope.SetFlag(flagName, enabled);
        return enabled;
    }

    private void LogPlan(DocumentVisibilityPlan plan)
    {
        if (plan.Decisions.Count == 0)
        {
            Log.NoGatedElements(_logger, plan.Mode);
            return;
        }

        var hidden = 0;

        foreach (var decision in plan.Decisions)
        {
            if (decision.Hidden)
            {
                hidden++;
            }
        }

        Log.DocumentGenerated(
            _logger,
            plan.Mode,
            hidden,
            plan.Decisions.Count - hidden,
            plan.DocumentFlags);
    }
}
