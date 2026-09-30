namespace OpenApiFeatureFlags;

/// <summary>
/// The engine-agnostic outcome of evaluating every <see cref="OpenApiFeatureFlagAttribute"/>
/// that applies to one document.
/// </summary>
/// <remarks>
/// <para>
/// The core produces this object and each document-engine adapter applies it. The plan never
/// references a document model, which is what allows an NSwag or
/// <c>Microsoft.AspNetCore.OpenApi</c> adapter to be added without touching the core.
/// </para>
/// <para>
/// Mode semantics are enforced here rather than trusted from the caller: in
/// <see cref="DocumentMode.Annotate"/> nothing is hidden, and in
/// <see cref="DocumentMode.Include"/> the plan is a no-op. A bug in an adapter therefore cannot
/// turn an annotate-only configuration into a destructive one.
/// </para>
/// </remarks>
public sealed class DocumentVisibilityPlan
{
    private readonly Dictionary<string, DocumentVisibilityDecision> _decisions;

    /// <summary>
    /// Gets the plan used when feature flags should not affect the document at all.
    /// </summary>
    public static DocumentVisibilityPlan NoOp { get; } = new(DocumentMode.Include, decisions: null);

    /// <summary>
    /// Initialises a new instance of the <see cref="DocumentVisibilityPlan"/> class.
    /// </summary>
    /// <param name="mode">The configured document mode.</param>
    /// <param name="decisions">
    /// Every decision the core reached, including the ones where all flags were enabled. May be
    /// <see langword="null"/> when there is nothing to apply.
    /// </param>
    public DocumentVisibilityPlan(DocumentMode mode, IEnumerable<DocumentVisibilityDecision>? decisions)
    {
        Mode = mode;
        IsNoOp = mode == DocumentMode.Include;

        var byKey = new Dictionary<string, DocumentVisibilityDecision>(StringComparer.Ordinal);
        var documentFlags = new SortedSet<string>(StringComparer.Ordinal);

        if (!IsNoOp && decisions is not null)
        {
            foreach (var decision in decisions)
            {
                ArgumentNullException.ThrowIfNull(decision);

                foreach (var flag in decision.Flags)
                {
                    documentFlags.Add(flag);
                }

                // Only Remove mode deletes anything; Annotate keeps the element and tags it.
                var hidden = mode == DocumentMode.Remove && decision.Hidden;

                byKey[decision.ElementKey] = byKey.TryGetValue(decision.ElementKey, out var existing)
                    ? new DocumentVisibilityDecision(
                        existing.Kind,
                        decision.ElementKey,
                        existing.Hidden || hidden,
                        [.. existing.Flags, .. decision.Flags])
                    : new DocumentVisibilityDecision(decision.Kind, decision.ElementKey, hidden, decision.Flags);
            }
        }

        _decisions = byKey;
        Decisions = [.. byKey.Values];
        DocumentFlags = [.. documentFlags];
    }

    /// <summary>Gets the configured document mode.</summary>
    public DocumentMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether the plan does nothing and the document must be published
    /// exactly as the engine generated it.
    /// </summary>
    public bool IsNoOp { get; }

    /// <summary>Gets every element the planner reached a decision about.</summary>
    public IReadOnlyList<DocumentVisibilityDecision> Decisions { get; }

    /// <summary>
    /// Gets the distinct flags that govern this document, in ordinal order. In
    /// <see cref="DocumentMode.Annotate"/> this drives the document-level <c>x-feature-flag</c> map.
    /// </summary>
    public IReadOnlyCollection<string> DocumentFlags { get; }

    /// <summary>
    /// Determines whether the element with the given canonical key must be removed from the document.
    /// </summary>
    /// <param name="elementKey">A key produced by <see cref="OperationKey.ToString"/> or <see cref="MemberKey.ToString"/>.</param>
    /// <returns><see langword="true"/> when the element must be removed.</returns>
    public bool IsHidden(string elementKey) =>
        elementKey is not null
        && _decisions.TryGetValue(elementKey, out var decision)
        && decision.Hidden;

    /// <summary>
    /// Gets the flags that govern the element with the given canonical key.
    /// </summary>
    /// <param name="elementKey">A key produced by <see cref="OperationKey.ToString"/> or <see cref="MemberKey.ToString"/>.</param>
    /// <returns>The governing flags, or an empty list when the element is not gated.</returns>
    public IReadOnlyList<string> FlagsFor(string elementKey) =>
        elementKey is not null && _decisions.TryGetValue(elementKey, out var decision)
            ? decision.Flags
            : [];

    /// <summary>Determines whether the given operation must be removed from the document.</summary>
    /// <param name="operation">The operation to test.</param>
    /// <returns><see langword="true"/> when the operation must be removed.</returns>
    public bool IsOperationHidden(OperationKey operation) => IsHidden(operation.ToString());

    /// <summary>Determines whether the given schema member or parameter must be removed from the document.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><see langword="true"/> when the member must be removed.</returns>
    public bool IsMemberHidden(MemberKey member) => IsHidden(member.ToString());

    /// <summary>
    /// Produces the single human-readable line that is logged once per document generation.
    /// </summary>
    /// <returns>A summary of what was hidden, what stayed, and which flags were involved.</returns>
    public string ToLogSummary()
    {
        if (IsNoOp)
        {
            return "OpenApiFeatureFlags: mode Include; feature flags ignored and the full document is published.";
        }

        if (Decisions.Count == 0)
        {
            return "OpenApiFeatureFlags: no gated elements in this document.";
        }

        var hidden = 0;
        var published = 0;

        foreach (var decision in Decisions)
        {
            if (decision.Hidden)
            {
                hidden++;
            }
            else
            {
                published++;
            }
        }

        var flags = DocumentFlags.Count == 0
            ? "none"
            : string.Join(", ", DocumentFlags);

        return $"OpenApiFeatureFlags: mode {Mode}; {hidden} hidden, {published} published; flags involved: {flags}.";
    }
}
