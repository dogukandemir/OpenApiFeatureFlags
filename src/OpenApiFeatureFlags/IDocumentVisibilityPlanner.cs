namespace OpenApiFeatureFlags;

/// <summary>
/// Turns feature-flag state into decisions about the generated document, and assembles those
/// decisions into an engine-agnostic <see cref="DocumentVisibilityPlan"/> (decision D9).
/// </summary>
/// <remarks>
/// <para>
/// Adapters drive this in three steps, because document engines invoke their filters one element at
/// a time and only hand over the whole document at the end:
/// </para>
/// <list type="number">
///   <item><description><see cref="IsHidden"/> per operation and per schema member, while the engine
///   builds the document. The adapter applies the answer where it can.</description></item>
///   <item><description><see cref="RecordDecision"/> for each element, so the plan can report what
///   happened.</description></item>
///   <item><description><see cref="CompleteDocument"/> once at the end, which returns the plan, logs
///   a single summary line and enforces the canary guard from D6.</description></item>
/// </list>
/// <para>
/// Implementations are registered as singletons, so they must hold no per-document state in fields
/// (invariant 4).
/// </para>
/// </remarks>
public interface IDocumentVisibilityPlanner
{
    /// <summary>
    /// Gets the configured document mode.
    /// </summary>
    DocumentMode Mode { get; }

    /// <summary>
    /// Evaluates a single flag, independently of <see cref="Mode"/>.
    /// </summary>
    /// <param name="flagName">The flag to evaluate.</param>
    /// <returns><see langword="true"/> when the flag is enabled.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="IsHidden"/> answers a <i>document</i> question and therefore short-circuits in
    /// <see cref="DocumentMode.Annotate"/> and <see cref="DocumentMode.Include"/>. Description fragments
    /// are different: gating a sentence is not the same decision as annotating the document, so
    /// adapters ask this instead.
    /// </para>
    /// <para>
    /// Fail-closed (D5) and memoised per request, exactly like <see cref="IsHidden"/>; an unevaluable
    /// flag counts towards the D6 canary.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="flagName"/> is null, empty or whitespace.</exception>
    bool IsFlagEnabled(string flagName);

    /// <summary>
    /// Determines whether an element carrying the given flags must be hidden from the document.
    /// </summary>
    /// <param name="flagNames">
    /// The flags governing the element. An empty list means the element is not gated.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the element must be hidden. In <see cref="DocumentMode.Annotate"/>
    /// and <see cref="DocumentMode.Include"/> this is always <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Flags are combined with <b>AND</b> semantics (D3): the element is hidden unless every flag is
    /// enabled. A flag that cannot be evaluated is treated as disabled, so the document fails closed
    /// (D5). Results are memoised for the current request.
    /// </remarks>
    bool IsHidden(IReadOnlyList<string> flagNames);

    /// <summary>
    /// Records what happened to one document element so the plan can report it.
    /// </summary>
    /// <param name="decision">The decision, including elements that stayed visible.</param>
    void RecordDecision(DocumentVisibilityDecision decision);

    /// <summary>
    /// Finishes the current document generation: returns the plan, writes the single summary log
    /// line and applies the D6 canary guard.
    /// </summary>
    /// <returns>The plan for the document that just finished.</returns>
    /// <exception cref="FeatureFlagSourceUnavailableException">
    /// The document contains gated elements but no flag could be evaluated.
    /// </exception>
    DocumentVisibilityPlan CompleteDocument();
}
