namespace OpenApiFeatureFlags;

/// <summary>
/// One decision the planner reached about one document element.
/// </summary>
/// <remarks>
/// A decision is always recorded, whether the element ends up hidden or annotated, because
/// the decision list is what the structured log line reports (Phase 2). A non-empty
/// <see cref="Flags"/> list with <see cref="Hidden"/> set to <see langword="false"/> means
/// the element was gated but every flag resolved to enabled.
/// </remarks>
public sealed class DocumentVisibilityDecision
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DocumentVisibilityDecision"/> class.
    /// </summary>
    /// <param name="kind">The kind of element the decision applies to.</param>
    /// <param name="elementKey">
    /// The canonical key of the element: <c>"GET /api/orders/{id}"</c> for operations,
    /// <c>"Namespace.Type.Member"</c> for members.
    /// </param>
    /// <param name="hidden"><see langword="true"/> when the element must be removed from the document.</param>
    /// <param name="flags">The flags that govern the element. Never <see langword="null"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="elementKey"/> is null, empty or whitespace.</exception>
    public DocumentVisibilityDecision(
        DocumentElementKind kind,
        string elementKey,
        bool hidden,
        IReadOnlyList<string>? flags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementKey);
        Kind = kind;
        ElementKey = elementKey;
        Hidden = hidden;
        Flags = flags ?? [];
    }

    /// <summary>Gets the kind of element the decision applies to.</summary>
    public DocumentElementKind Kind { get; }

    /// <summary>Gets the canonical key of the element.</summary>
    public string ElementKey { get; }

    /// <summary>Gets a value indicating whether the element must be removed from the document.</summary>
    public bool Hidden { get; }

    /// <summary>Gets the flags that govern the element.</summary>
    public IReadOnlyList<string> Flags { get; }
}
