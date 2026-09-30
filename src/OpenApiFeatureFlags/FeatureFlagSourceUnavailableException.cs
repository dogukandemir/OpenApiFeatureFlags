namespace OpenApiFeatureFlags;

/// <summary>
/// Thrown when a document contains gated elements but the flag source could not be reached for
/// any of them, so the generated document cannot be trusted (decision D6).
/// </summary>
/// <remarks>
/// This exception deliberately surfaces as a failure rather than degrading to "hide everything".
/// A published document that is silently missing released endpoints is far more damaging than a
/// build or a request that fails loudly.
/// </remarks>
public sealed class FeatureFlagSourceUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="FeatureFlagSourceUnavailableException"/> class.
    /// </summary>
    public FeatureFlagSourceUnavailableException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="FeatureFlagSourceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The failure description.</param>
    public FeatureFlagSourceUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="FeatureFlagSourceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying failure.</param>
    public FeatureFlagSourceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="FeatureFlagSourceUnavailableException"/> class.
    /// </summary>
    /// <param name="attemptedFlagCount">How many flags were requested before giving up.</param>
    public FeatureFlagSourceUnavailableException(int attemptedFlagCount)
        : base(DefaultMessage)
    {
        AttemptedFlagCount = attemptedFlagCount;
    }

    /// <summary>
    /// Gets how many flag evaluations were attempted before the planner gave up.
    /// </summary>
    public int AttemptedFlagCount { get; }

    private const string DefaultMessage =
        "OpenApiFeatureFlags could not evaluate any feature flag for the generated document, so the " +
        "document cannot be trusted: fail-closed would have removed released endpoints. Check that " +
        "IFeatureFlagSource is configured and reachable, or set " +
        "OpenApiFeatureFlagsOptions.CanaryEnabled to false to accept the risk (decision D6).";
}
