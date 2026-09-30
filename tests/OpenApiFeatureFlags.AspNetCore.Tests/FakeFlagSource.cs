namespace OpenApiFeatureFlags.AspNetCore.Tests;

/// <summary>A flag source that answers from a fixed set of enabled names.</summary>
internal sealed class FakeFlagSource : IFeatureFlagSource
{
    private readonly HashSet<string> _enabled;

    public FakeFlagSource(params string[] enabled) =>
        _enabled = new HashSet<string>(enabled, StringComparer.Ordinal);

    public bool IsEnabled(string flagName) => _enabled.Contains(flagName);
}

/// <summary>
/// A flag source that cannot answer at all, for the fail-closed and canary paths.
/// </summary>
internal sealed class UnavailableFlagSource : IFeatureFlagSource
{
    public bool IsEnabled(string flagName) =>
        throw new InvalidOperationException($"The store cannot answer for '{flagName}'.");
}
