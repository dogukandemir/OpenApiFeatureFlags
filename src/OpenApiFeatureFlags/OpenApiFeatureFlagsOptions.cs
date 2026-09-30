namespace OpenApiFeatureFlags;

/// <summary>
/// Configuration for OpenApiFeatureFlags.
/// </summary>
public sealed class OpenApiFeatureFlagsOptions
{
    /// <summary>
    /// Gets or sets how a flag that is not enabled affects the document.
    /// Defaults to <see cref="DocumentMode.Remove"/>.
    /// </summary>
    public DocumentMode Mode { get; set; } = DocumentMode.Remove;

    /// <summary>
    /// Gets or sets a value indicating whether the canary guard is active.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Failing closed is only safe while the flag source is actually reachable. If the source
    /// is down, every flag reads as "disabled" and a fail-closed filter would quietly <i>delete
    /// released endpoints</i> from a published document — the opposite of the bug this library
    /// fixes, and worse, because it is silent.
    /// </para>
    /// <para>
    /// When this is enabled and a document contains gated elements but <b>not one flag could be
    /// evaluated</b>, the planner throws <see cref="FeatureFlagSourceUnavailableException"/> instead
    /// of publishing a depleted document.
    /// </para>
    /// </remarks>
    public bool CanaryEnabled { get; set; } = true;
}
