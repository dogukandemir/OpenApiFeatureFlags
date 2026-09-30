namespace OpenApiFeatureFlags;

/// <summary>
/// Describes how a feature flag affects the generated OpenAPI document.
/// </summary>
public enum DocumentMode
{
    /// <summary>
    /// Gated elements are removed from the document. This is the default: it fixes the
    /// original problem where customers saw documented API surface they could not use.
    /// </summary>
    Remove = 0,

    /// <summary>
    /// Gated elements stay in the document but carry an <c>x-feature-flag</c> extension, so
    /// portals and client generators can filter them without the document being destructive.
    /// </summary>
    Annotate = 1,

    /// <summary>
    /// Feature flags are ignored entirely and the full document is published. This is the
    /// per-environment off switch, useful when generating reference docs for an internal
    /// audience.
    /// </summary>
    Include = 2,
}
