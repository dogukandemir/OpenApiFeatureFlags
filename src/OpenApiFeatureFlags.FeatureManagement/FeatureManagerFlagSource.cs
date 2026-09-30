using Microsoft.FeatureManagement;

namespace OpenApiFeatureFlags.FeatureManagement;

/// <summary>
/// Adapts Microsoft's <see cref="IFeatureManager"/> to <see cref="IFeatureFlagSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately does not catch anything. The core already treats a failed read as "not enabled"
/// and letting the exception through is what allows the canary to notice that the flag
/// store is unreachable — a caught-and-swallowed failure would look exactly like "every flag is off",
/// which is the dangerous case.
/// </para>
/// <para>
/// <see cref="IFeatureManager"/> is asynchronous while document engines expose a synchronous
/// pipeline, so the call is blocked on. Microsoft's implementation completes from an in-memory
/// snapshot in the common case, and <c>IFeatureManager</c> caches per request. If a provider turns
/// out to be genuinely slow here, that is the trigger for an async resolution path
/// rather than a reason to start leaking tasks from a filter.
/// </para>
/// </remarks>
public sealed class FeatureManagerFlagSource : IFeatureFlagSource
{
    /// <summary>
    /// The value Microsoft's <c>IFeatureManager</c> reports when a feature is not defined at all.
    /// </summary>
    public const bool MissingFeatureDefault = false;

    private readonly IFeatureManager _featureManager;

    /// <summary>
    /// Initialises a new instance of the <see cref="FeatureManagerFlagSource"/> class.
    /// </summary>
    /// <param name="featureManager">The feature manager to delegate to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="featureManager"/> is <see langword="null"/>.</exception>
    public FeatureManagerFlagSource(IFeatureManager featureManager)
    {
        ArgumentNullException.ThrowIfNull(featureManager);
        _featureManager = featureManager;
    }

    /// <inheritdoc />
    public bool IsEnabled(string flagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagName);

        return _featureManager.IsEnabledAsync(flagName).GetAwaiter().GetResult();
    }
}
