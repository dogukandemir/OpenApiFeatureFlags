namespace OpenApiFeatureFlags;

/// <summary>
/// The seam between the document engines and whatever evaluates feature flags
/// (decision D8: the flag source is an independent axis from the document engine).
/// </summary>
/// <remarks>
/// The interface is synchronous because every supported document engine generates its
/// document synchronously. Flag providers that are async-only are adapted by their
/// integration package, not by widening this contract (open question Q4).
/// </remarks>
public interface IFeatureFlagSource
{
    /// <summary>
    /// Evaluates whether the named flag is enabled.
    /// </summary>
    /// <param name="flagName">The flag name exactly as written on <see cref="OpenApiFeatureFlagAttribute"/>.</param>
    /// <returns><see langword="true"/> when the flag is enabled; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Throwing is a supported response. Callers treat any exception as "not enabled" so the
    /// document fails closed (decision D5): an unreachable flag source must hide unreleased
    /// surface, never leak it.
    /// </remarks>
    bool IsEnabled(string flagName);
}
