using Microsoft.AspNetCore.Http;

namespace OpenApiFeatureFlags;

/// <summary>
/// The working state of one document generation.
/// </summary>
/// <remarks>
/// <para>
/// This is the implementation of invariant 4: flag state and decisions live here, never in a field
/// of a filter. Swashbuckle builds filters once for the lifetime of the application, so a field
/// would hold the first request's snapshot forever.
/// </para>
/// <para>
/// When a request is in flight the scope is parked in <c>HttpContext.Items</c>, so it dies with the
/// request. When there is none (offline export, for example <c>swagger tofile</c>) the caller
/// supplies a fallback scope; that path is a single pass, and
/// <see cref="DocumentVisibilityPlanner.CompleteDocument"/> empties it.
/// </para>
/// </remarks>
internal sealed class RequestScope
{
    private static readonly object HttpContextKey = new();

    private readonly Dictionary<string, bool> _flags = new(StringComparer.Ordinal);
    private readonly List<DocumentVisibilityDecision> _decisions = [];

    internal RequestScope()
    {
    }

    /// <summary>Gets how many flag reads were attempted for the current document.</summary>
    public int AttemptedEvaluations { get; private set; }

    /// <summary>Gets how many of those reads returned without throwing.</summary>
    public int SuccessfulEvaluations { get; private set; }

    /// <summary>
    /// Gets the scope for the current request, creating it on first use, or the supplied fallback
    /// when there is no <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="httpContext">The current request context, if any.</param>
    /// <param name="fallback">The scope to use when <paramref name="httpContext"/> is <see langword="null"/>.</param>
    /// <returns>The scope for this document generation.</returns>
    public static RequestScope For(HttpContext? httpContext, RequestScope fallback)
    {
        if (httpContext is null)
        {
            return fallback;
        }

        if (httpContext.Items.TryGetValue(HttpContextKey, out var existing) && existing is RequestScope scope)
        {
            return scope;
        }

        var created = new RequestScope();
        httpContext.Items[HttpContextKey] = created;
        return created;
    }

    /// <summary>Reads a memoised flag value.</summary>
    /// <param name="flagName">The flag name.</param>
    /// <param name="enabled">The memoised value when the flag has already been read.</param>
    /// <returns><see langword="true"/> when the value was already known.</returns>
    public bool TryGetFlag(string flagName, out bool enabled) => _flags.TryGetValue(flagName, out enabled);

    /// <summary>Memoises a flag value for the rest of the request.</summary>
    /// <param name="flagName">The flag name.</param>
    /// <param name="enabled">The resolved value.</param>
    public void SetFlag(string flagName, bool enabled) => _flags[flagName] = enabled;

    /// <summary>Counts one attempted flag read.</summary>
    public void CountAttempt() => AttemptedEvaluations++;

    /// <summary>Counts one flag read that completed without throwing.</summary>
    public void CountSuccess() => SuccessfulEvaluations++;

    /// <summary>Records a decision about one document element.</summary>
    /// <param name="decision">The decision.</param>
    public void Add(DocumentVisibilityDecision decision) => _decisions.Add(decision);

    /// <summary>
    /// Produces the plan for the document that just finished and clears the per-document state, so
    /// nothing can leak into the next document.
    /// </summary>
    /// <param name="mode">The configured document mode.</param>
    /// <param name="keepMemoisedFlags">
    /// <see langword="true"/> to keep memoised flag values for the rest of the request. This must be
    /// <see langword="false"/> on the offline path, where the fallback scope outlives the document
    /// and a retained value would be a stale snapshot.
    /// </param>
    /// <returns>The plan for the finished document.</returns>
    public DocumentVisibilityPlan BuildPlanAndReset(DocumentMode mode, bool keepMemoisedFlags)
    {
        var plan = new DocumentVisibilityPlan(mode, _decisions);
        _decisions.Clear();
        AttemptedEvaluations = 0;
        SuccessfulEvaluations = 0;

        if (!keepMemoisedFlags)
        {
            _flags.Clear();
        }

        return plan;
    }
}
